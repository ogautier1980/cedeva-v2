using Cedeva.Core.DTOs;

namespace Cedeva.Core.Interfaces;

/// <summary>
/// Fiscal attestation for childcare expenses (form 281.86): one per parent and per year, all the
/// organisation's activities together. Amounts are the payments made during the calendar year.
/// </summary>
public interface IFiscalAttestationService
{
    /// <summary>Years in which the parent made payments, most recent first.</summary>
    Task<IReadOnlyList<int>> GetYearsAsync(int parentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The attestation of <paramref name="parentId"/> for <paramref name="year"/>, or null when the
    /// parent is not visible to the current user. Children without any eligible day are left out.
    /// </summary>
    Task<FiscalAttestationData?> BuildAsync(int parentId, int year, CancellationToken cancellationToken = default);

    /// <summary>Parents of the organisation that have at least one eligible line in the year.</summary>
    Task<IReadOnlyList<FiscalAttestationData>> BuildAllAsync(int organisationId, int year, CancellationToken cancellationToken = default);

    /// <summary>The 4-page PDF (letter, explanations, Cadre I, one Cadre II per child).</summary>
    byte[] RenderPdf(FiscalAttestationData data);
}
