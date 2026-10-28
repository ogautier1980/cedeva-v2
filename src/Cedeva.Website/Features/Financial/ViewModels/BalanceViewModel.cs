namespace Cedeva.Website.Features.Financial.ViewModels;

/// <summary>BILAN de l'activité : deux sections (Entrées / Dépenses) de lignes par catégorie.</summary>
public class BalanceViewModel
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public string OrganisationName { get; set; } = string.Empty;
    public string? OrganisationAddress { get; set; }
    public string? CompanyNumber { get; set; }

    public List<BalanceLineViewModel> IncomeLines { get; set; } = new();
    public List<BalanceLineViewModel> ExpenseLines { get; set; } = new();

    public decimal TotalIncomings => IncomeLines.Sum(l => l.Incomings) + ExpenseLines.Sum(l => l.Incomings);
    public decimal TotalOutgoings => IncomeLines.Sum(l => l.Outgoings) + ExpenseLines.Sum(l => l.Outgoings);
    public decimal Result => TotalIncomings - TotalOutgoings;
}

public class BalanceLineViewModel
{
    public string Name { get; set; } = string.Empty;
    public decimal Incomings { get; set; }
    public decimal Outgoings { get; set; }
    public decimal Bilan => Incomings - Outgoings;

    /// <summary>Budget estimé au départ (null si non renseigné, ou ligne calculée sans catégorie).</summary>
    public decimal? Budget { get; set; }

    /// <summary>Line computed by the app (P.A.F., team compensation, uncategorised) rather than a category.</summary>
    public bool IsFixed { get; set; }
}
