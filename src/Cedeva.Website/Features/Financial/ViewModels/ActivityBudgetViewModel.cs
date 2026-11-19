namespace Cedeva.Website.Features.Financial.ViewModels;

/// <summary>The activity's Budget page: one line per category (Entrées / Sorties), Hors bilan excluded.</summary>
public class ActivityBudgetViewModel
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public List<ActivityBudgetLineViewModel> Lines { get; set; } = new();
}

public class ActivityBudgetLineViewModel
{
    public int ExpenseCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsIncome { get; set; }

    /// <summary>This activity's budget; null = none.</summary>
    public decimal? Amount { get; set; }

    /// <summary>The organisation's template budget for the category, shown for reference.</summary>
    public decimal? TemplateAmount { get; set; }
}
