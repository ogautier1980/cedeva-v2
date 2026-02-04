using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Infrastructure.Data;

public class CedevaDbContext : IdentityDbContext<CedevaUser>, IUnitOfWork
{
    private readonly ICurrentUserService? _currentUserService;

    public CedevaDbContext(DbContextOptions<CedevaDbContext> options) : base(options)
    {
    }

    public CedevaDbContext(DbContextOptions<CedevaDbContext> options, ICurrentUserService currentUserService)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<Organisation> Organisations => Set<Organisation>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ActivityDay> ActivityDays => Set<ActivityDay>();
    public DbSet<ActivityGroup> ActivityGroups => Set<ActivityGroup>();
    public DbSet<ActivityQuestion> ActivityQuestions => Set<ActivityQuestion>();
    public DbSet<ActivityQuestionAnswer> ActivityQuestionAnswers => Set<ActivityQuestionAnswer>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<BelgianMunicipality> BelgianMunicipalities => Set<BelgianMunicipality>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingDay> BookingDays => Set<BookingDay>();
    public DbSet<Child> Children => Set<Child>();
    public DbSet<Parent> Parents => Set<Parent>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<EmailSent> EmailsSent => Set<EmailSent>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<CodaFile> CodaFiles => Set<CodaFile>();
    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();
    public DbSet<ActivityFinancialTransaction> ActivityFinancialTransactions => Set<ActivityFinancialTransaction>();
    public DbSet<Excursion> Excursions => Set<Excursion>();
    public DbSet<ExcursionRegistration> ExcursionRegistrations => Set<ExcursionRegistration>();
    public DbSet<ExcursionGroup> ExcursionGroups => Set<ExcursionGroup>();
    public DbSet<ExcursionTeamMember> ExcursionTeamMembers => Set<ExcursionTeamMember>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply all configurations from assembly
        builder.ApplyConfigurationsFromAssembly(typeof(CedevaDbContext).Assembly);

        // Multi-tenancy global query filters
        ConfigureMultiTenancyFilters(builder);
    }

    private void ConfigureMultiTenancyFilters(ModelBuilder builder)
    {
        // Filter activities by organisation
        builder.Entity<Activity>()
            .HasQueryFilter(a => _currentUserService == null ||
                                 _currentUserService.IsAdmin ||
                                 a.OrganisationId == _currentUserService.OrganisationId);

        // Filter parents by organisation
        builder.Entity<Parent>()
            .HasQueryFilter(p => _currentUserService == null ||
                                 _currentUserService.IsAdmin ||
                                 p.OrganisationId == _currentUserService.OrganisationId);

        // Filter team members by organisation
        builder.Entity<TeamMember>()
            .HasQueryFilter(t => _currentUserService == null ||
                                 _currentUserService.IsAdmin ||
                                 t.OrganisationId == _currentUserService.OrganisationId);

        // Filter CODA files by organisation
        builder.Entity<CodaFile>()
            .HasQueryFilter(cf => _currentUserService == null ||
                                  _currentUserService.IsAdmin ||
                                  cf.OrganisationId == _currentUserService.OrganisationId);

        // Filter bank transactions by organisation
        builder.Entity<BankTransaction>()
            .HasQueryFilter(bt => _currentUserService == null ||
                                  _currentUserService.IsAdmin ||
                                  bt.OrganisationId == _currentUserService.OrganisationId);

        // Filter email templates by organisation
        builder.Entity<EmailTemplate>()
            .HasQueryFilter(et => _currentUserService == null ||
                                  _currentUserService.IsAdmin ||
                                  et.OrganisationId == _currentUserService.OrganisationId);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await base.SaveChangesAsync(cancellationToken);
    }

    async Task<int> IUnitOfWork.SaveChangesAsync()
    {
        return await SaveChangesAsync();
    }
}
