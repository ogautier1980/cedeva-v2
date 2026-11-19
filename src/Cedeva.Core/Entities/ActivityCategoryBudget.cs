namespace Cedeva.Core.Entities;

/// <summary>
/// Budget of one category for one activity (Thomas, 19/11/2026: « chaque stage peut avoir son
/// budget »). Copied from the organisation's template — the budget set on the category,
/// <see cref="ExpenseCategory.Budget"/> — when the activity is created or on demand, then edited
/// per activity. Shown next to the actual amounts on the BILAN.
/// </summary>
public class ActivityCategoryBudget : AuditableEntity
{
    public int Id { get; set; }

    public int ActivityId { get; set; }
    public Activity Activity { get; set; } = null!;

    public int ExpenseCategoryId { get; set; }
    public ExpenseCategory ExpenseCategory { get; set; } = null!;

    public decimal Amount { get; set; }
}
