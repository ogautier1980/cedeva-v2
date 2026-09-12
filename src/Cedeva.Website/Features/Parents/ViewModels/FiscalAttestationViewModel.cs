namespace Cedeva.Website.Features.Parents.ViewModels;

/// <summary>
/// Lot H — attestation fiscale (frais de garde d'enfants, art. 113 CIR92), groupée par association
/// (une par parent, tous ses enfants et toutes les activités de l'organisation confondus pour
/// l'année fiscale demandée) — à la différence de l'attestation mutuelle (Lot K #3), qui est par
/// enfant/activité. ⚠️ Aucun exemple réel fourni par le client : mentions légales génériques
/// belges, à faire relire par un comptable/Thomas avant tout usage réel (voir BACKLOG.md, Lot H).
/// </summary>
public class FiscalAttestationViewModel
{
    public int ParentId { get; set; }
    public int FiscalYear { get; set; }
    public List<int> AvailableYears { get; set; } = new();

    public string OrganisationName { get; set; } = string.Empty;
    public string? OrganisationLogoUrl { get; set; }
    public string OrganisationAddress { get; set; } = string.Empty;
    public string? ResponsibleName { get; set; }
    public string? CompanyNumber { get; set; }

    public string ParentFirstName { get; set; } = string.Empty;
    public string ParentLastName { get; set; } = string.Empty;
    public string ParentNationalRegisterNumber { get; set; } = string.Empty;
    public string ParentAddress { get; set; } = string.Empty;

    public List<FiscalAttestationLine> Lines { get; set; } = new();
    public decimal TotalAmountPaid => Lines.Sum(l => l.AmountPaid);

    public DateTime IssuedDate { get; set; } = DateTime.Today;
}

public class FiscalAttestationLine
{
    public string ChildFirstName { get; set; } = string.Empty;
    public string ChildLastName { get; set; } = string.Empty;
    public DateTime ChildBirthDate { get; set; }
    public string ChildNationalRegisterNumber { get; set; } = string.Empty;

    public string ActivityName { get; set; } = string.Empty;
    public DateTime ActivityStartDate { get; set; }
    public DateTime ActivityEndDate { get; set; }

    public decimal AmountPaid { get; set; }
}
