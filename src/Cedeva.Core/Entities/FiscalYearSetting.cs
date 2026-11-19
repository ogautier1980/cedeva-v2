using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

/// <summary>
/// Childcare tax-deduction figures for one income year, printed on the fiscal attestation
/// (form 281.86) of every organisation: the indexed daily ceiling and the tax-return code.
/// Global (not organisation-scoped), maintained by the admin.
/// </summary>
public class FiscalYearSetting : AuditableEntity
{
    public int Id { get; set; }

    /// <summary>Year of the expenses (e.g. 2025 for the attestation sent in 2026).</summary>
    public int Year { get; set; }

    /// <summary>Maximum deductible amount per day of care and per child (e.g. 16.90 € for 2025).</summary>
    public decimal MaxDailyAmount { get; set; }

    /// <summary>Code of the tax return where the expenses are entered (1384 for 2025).</summary>
    [MaxLength(10)]
    public string TaxCode { get; set; } = "1384";
}
