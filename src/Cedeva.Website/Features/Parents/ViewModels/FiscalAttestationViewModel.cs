using Cedeva.Core.DTOs;

namespace Cedeva.Website.Features.Parents.ViewModels;

/// <summary>
/// The parent's fiscal attestation page (form 281.86, model sent by the client on 19/11/2026):
/// year choice, what the PDF will certify, and the PDF / e-mail actions. Distinct from the
/// mutuality attestation, which is per child and activity.
/// </summary>
public class FiscalAttestationViewModel
{
    public int ParentId { get; set; }
    public int FiscalYear { get; set; }
    public IReadOnlyList<int> AvailableYears { get; set; } = [];
    public FiscalAttestationData Data { get; set; } = new();
}
