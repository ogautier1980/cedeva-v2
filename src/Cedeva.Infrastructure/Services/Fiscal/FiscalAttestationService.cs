using Cedeva.Core.DTOs;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Infrastructure.Services.Pdf;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Infrastructure.Services.Fiscal;

/// <summary>
/// Builds the fiscal attestation (form 281.86) of a parent for a year. Rules (client's choices of
/// 19/11/2026): amounts = payments dated in the calendar year; days = days marked present, only
/// before the child's 14th birthday (form note 5), the amount being prorated when some present days
/// fall after it; one document per parent, one Cadre II per child, one line per activity.
/// </summary>
public class FiscalAttestationService : IFiscalAttestationService
{
    private const int AgeLimit = 14;

    private readonly CedevaDbContext _context;
    private readonly IStorageService _storageService;

    public FiscalAttestationService(CedevaDbContext context, IStorageService storageService)
    {
        _context = context;
        _storageService = storageService;
    }

    public async Task<IReadOnlyList<int>> GetYearsAsync(int parentId, CancellationToken cancellationToken = default)
    {
        return await PaidPayments()
            .Where(p => p.Booking.Child.ParentId == parentId)
            .Select(p => p.PaymentDate.Year)
            .Distinct()
            .OrderByDescending(y => y)
            .ToListAsync(cancellationToken);
    }

    public async Task<FiscalAttestationData?> BuildAsync(int parentId, int year, CancellationToken cancellationToken = default)
    {
        // Tenancy filter on: a coordinator only reaches parents of their own organisation.
        var parent = await _context.Parents
            .Include(p => p.Address)
            .FirstOrDefaultAsync(p => p.Id == parentId, cancellationToken);
        if (parent == null)
        {
            return null;
        }

        var payments = await LoadPaymentsAsync(year, p => p.Booking.Child.ParentId == parentId, cancellationToken);
        var context = await LoadYearContextAsync(parent.OrganisationId, year, cancellationToken);
        return Build(parent, payments, year, context);
    }

    public async Task<IReadOnlyList<FiscalAttestationData>> BuildAllAsync(int organisationId, int year, CancellationToken cancellationToken = default)
    {
        var payments = await LoadPaymentsAsync(year, p => p.Booking.Child.Parent.OrganisationId == organisationId, cancellationToken);
        var parentIds = payments.Select(p => p.Booking.Child.ParentId).Distinct().ToList();
        var parents = await _context.Parents.IgnoreQueryFilters()
            .Include(p => p.Address)
            .Where(p => parentIds.Contains(p.Id))
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .ToListAsync(cancellationToken);
        var context = await LoadYearContextAsync(organisationId, year, cancellationToken);

        return parents
            .Select(parent => Build(parent, payments.Where(p => p.Booking.Child.ParentId == parent.Id).ToList(), year, context))
            .Where(data => data.Children.Count > 0)
            .ToList();
    }

    public byte[] RenderPdf(FiscalAttestationData data) => FiscalAttestationPdf.Render(data);

    private IQueryable<Payment> PaidPayments() =>
        _context.Payments.IgnoreQueryFilters().Where(p => p.Status == PaymentStatus.Paid);

    private async Task<List<Payment>> LoadPaymentsAsync(int year, System.Linq.Expressions.Expression<Func<Payment, bool>> scope,
        CancellationToken cancellationToken)
    {
        var start = new DateTime(year, 1, 1);
        var end = start.AddYears(1);
        return await PaidPayments()
            .Where(scope)
            .Where(p => p.PaymentDate >= start && p.PaymentDate < end)
            .Include(p => p.Booking).ThenInclude(b => b.Activity)
            .Include(p => p.Booking).ThenInclude(b => b.Child)
            .Include(p => p.Booking).ThenInclude(b => b.Days).ThenInclude(d => d.ActivityDay)
            .ToListAsync(cancellationToken);
    }

    private sealed record YearContext(Organisation Organisation, FiscalYearSetting? Setting, byte[]? Logo, byte[]? Signature);

    private async Task<YearContext> LoadYearContextAsync(int organisationId, int year, CancellationToken cancellationToken)
    {
        var organisation = await _context.Organisations.IgnoreQueryFilters()
            .Include(o => o.Address)
            .FirstAsync(o => o.Id == organisationId, cancellationToken);
        var setting = await _context.FiscalYearSettings.FirstOrDefaultAsync(s => s.Year == year, cancellationToken);
        return new YearContext(organisation, setting,
            await ReadImageAsync(organisation.LogoUrl), await ReadImageAsync(organisation.ResponsibleSignatureUrl));
    }

    private static FiscalAttestationData Build(Parent parent, List<Payment> payments, int year, YearContext context)
    {
        var children = payments
            .GroupBy(p => p.Booking.Child)
            .Select(childPayments => BuildChild(childPayments.Key, childPayments.ToList(), parent))
            .Where(child => child.Periods.Count > 0)
            .OrderBy(child => child.FirstName)
            .ToList();

        var organisation = context.Organisation;
        var address = organisation.Address;
        return new FiscalAttestationData
        {
            Year = year,
            IssuedDate = DateTime.Today,
            Organisation = new FiscalAttestationOrganisation
            {
                Name = organisation.Name,
                Street = address?.Street ?? string.Empty,
                PostalCodeCity = address == null ? string.Empty : $"{address.PostalCode} {address.City}",
                City = address?.City ?? string.Empty,
                Phone = organisation.Phone1,
                ResponsibleName = organisation.ResponsibleName,
                ActivityNames = children.SelectMany(c => c.Periods).Select(p => p.ActivityName).Distinct().ToList(),
                Logo = context.Logo,
                Signature = context.Signature
            },
            ParentId = parent.Id,
            ParentFullName = $"{parent.FirstName} {parent.LastName}",
            ParentStreet = parent.Address?.Street ?? string.Empty,
            ParentPostalCodeCity = parent.Address == null ? string.Empty : $"{parent.Address.PostalCode} {parent.Address.City}",
            ParentEmail = parent.Email,
            Children = children,
            MaxDailyAmount = context.Setting?.MaxDailyAmount,
            TaxCode = context.Setting?.TaxCode
        };
    }

    private static FiscalAttestationChild BuildChild(Child child, List<Payment> payments, Parent parent)
    {
        var ageLimitDate = child.BirthDate.AddYears(AgeLimit);
        var periods = payments
            .GroupBy(p => p.Booking)
            .Select(bookingPayments =>
            {
                var booking = bookingPayments.Key;
                var presentDays = booking.Days.Where(d => d.IsPresent).Select(d => d.ActivityDay.DayDate.Date).ToList();
                var eligibleDays = presentDays.Where(d => d < ageLimitDate).OrderBy(d => d).ToList();
                if (eligibleDays.Count == 0)
                {
                    return null;
                }

                var paid = bookingPayments.Sum(p => p.Amount);
                // Days after the 14th birthday do not count: their share of the amount is left out.
                var amount = eligibleDays.Count == presentDays.Count
                    ? paid
                    : Math.Round(paid * eligibleDays.Count / presentDays.Count, 2);
                return new FiscalAttestationPeriod
                {
                    ActivityName = booking.Activity.Name,
                    From = eligibleDays[0],
                    To = eligibleDays[^1],
                    Days = eligibleDays.Count,
                    Amount = amount
                };
            })
            .OfType<FiscalAttestationPeriod>()
            .OrderBy(p => p.From)
            .ToList();

        return new FiscalAttestationChild
        {
            FirstName = child.FirstName,
            LastName = child.LastName,
            BirthDate = child.BirthDate,
            Address = parent.Address == null ? string.Empty : $"{parent.Address.Street} - {parent.Address.PostalCode} {parent.Address.City}",
            Periods = periods
        };
    }

    // Logo/signature stored by the app (/uploads/…); other URLs are not embedded in the PDF.
    private async Task<byte[]?> ReadImageAsync(string? url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith("/uploads/", StringComparison.Ordinal))
        {
            return null;
        }

        await using var stream = await _storageService.DownloadFileAsync(url);
        if (stream == null)
        {
            return null;
        }

        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        return memory.ToArray();
    }
}
