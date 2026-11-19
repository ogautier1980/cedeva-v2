using Cedeva.Core.DTOs;
using Cedeva.Core.Interfaces;
using Cedeva.Website.Localization;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Features.FiscalAttestations;

/// <summary>
/// E-mails one parent their fiscal attestation as a PDF attachment. Shared by the parent's page
/// (one at a time) and the organisation-wide sending page.
/// </summary>
public class FiscalAttestationSender
{
    private readonly IFiscalAttestationService _fiscalAttestationService;
    private readonly IEmailService _emailService;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public FiscalAttestationSender(
        IFiscalAttestationService fiscalAttestationService,
        IEmailService emailService,
        IStringLocalizer<SharedResources> localizer)
    {
        _fiscalAttestationService = fiscalAttestationService;
        _emailService = emailService;
        _localizer = localizer;
    }

    /// <summary>File name of the PDF, e.g. "Attestation-fiscale-2025-Dupont.pdf".</summary>
    public static string FileName(FiscalAttestationData data)
    {
        var lastName = data.ParentFullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "parent";
        var safeName = new string(lastName.Where(char.IsLetterOrDigit).ToArray());
        return $"Attestation-fiscale-{data.Year}-{safeName}.pdf";
    }

    /// <summary>Sends the attestation; false when the parent has no e-mail address.</summary>
    public async Task<bool> SendAsync(FiscalAttestationData data)
    {
        if (string.IsNullOrWhiteSpace(data.ParentEmail))
        {
            return false;
        }

        // IEmailService attaches a file from disk: write the PDF to a private temporary folder.
        var folder = Path.Combine(Path.GetTempPath(), "cedeva-fiscal", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, FileName(data));
            await File.WriteAllBytesAsync(path, _fiscalAttestationService.RenderPdf(data));

            var subject = _localizer["FiscalAttestation.EmailSubject", data.Year, data.Organisation.Name].Value;
            var body = _localizer["FiscalAttestation.EmailBody", data.Year, data.Organisation.Name].Value;
            await _emailService.SendEmailAsync(data.ParentEmail, subject, body, path);
            return true;
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
