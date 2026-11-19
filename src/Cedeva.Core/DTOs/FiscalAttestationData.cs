namespace Cedeva.Core.DTOs;

/// <summary>
/// Everything printed on one parent's fiscal attestation (form 281.86) for one year: one document
/// per parent, with a Cadre II per child gathering all of that child's activities of the year.
/// </summary>
public class FiscalAttestationData
{
    public int Year { get; init; }
    public DateTime IssuedDate { get; init; }

    public FiscalAttestationOrganisation Organisation { get; init; } = new();

    public int ParentId { get; init; }
    public string ParentFullName { get; init; } = string.Empty;
    public string ParentStreet { get; init; } = string.Empty;
    public string ParentPostalCodeCity { get; init; } = string.Empty;
    public string? ParentEmail { get; init; }

    public IReadOnlyList<FiscalAttestationChild> Children { get; init; } = [];

    /// <summary>Daily ceiling and tax code of the year; null when the admin has not set the year yet.</summary>
    public decimal? MaxDailyAmount { get; init; }
    public string? TaxCode { get; init; }

    public decimal Total => Children.Sum(c => c.Total);
}

public class FiscalAttestationOrganisation
{
    public string Name { get; init; } = string.Empty;
    public string Street { get; init; } = string.Empty;
    public string PostalCodeCity { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? ResponsibleName { get; init; }

    /// <summary>Names of the activities covered by the attestation (Cadre I).</summary>
    public IReadOnlyList<string> ActivityNames { get; init; } = [];

    public byte[]? Logo { get; init; }
    public byte[]? Signature { get; init; }
}

public class FiscalAttestationChild
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public DateTime BirthDate { get; init; }
    public string Address { get; init; } = string.Empty;

    /// <summary>One line per activity attended (form point 4: "Période 1", "Période 2"…).</summary>
    public IReadOnlyList<FiscalAttestationPeriod> Periods { get; init; } = [];

    public decimal Total => Periods.Sum(p => p.Amount);
}

public class FiscalAttestationPeriod
{
    public string ActivityName { get; init; } = string.Empty;
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public int Days { get; init; }
    public decimal Amount { get; init; }
}
