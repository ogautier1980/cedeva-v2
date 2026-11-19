using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Infrastructure.Services.Financial;

/// <summary>
/// The organisation's budget template — the budgets set on its categories (Entrées / Sorties) —
/// copied into an activity: automatically when the activity is created, and again on demand from
/// the activity's Budget page (Thomas: « un modèle à créer, à modifier puis à importer dans un
/// stage », like the e-mail templates).
/// </summary>
public static class ActivityBudgetTemplate
{
    /// <summary>
    /// Replaces the activity's budget with the template. Hors bilan categories carry no budget.
    /// Returns the number of budget lines copied. Query filters are bypassed: the caller has
    /// already resolved the activity.
    /// </summary>
    public static async Task<int> ApplyAsync(CedevaDbContext context, int activityId, CancellationToken cancellationToken = default)
    {
        var organisationId = await context.Activities.IgnoreQueryFilters()
            .Where(a => a.Id == activityId)
            .Select(a => a.OrganisationId)
            .FirstAsync(cancellationToken);

        var template = await context.ExpenseCategories.IgnoreQueryFilters()
            .Where(c => c.OrganisationId == organisationId && c.Budget != null && c.CategoryType != ExpenseCategoryType.OffBalance)
            .Select(c => new { c.Id, Budget = c.Budget!.Value })
            .ToListAsync(cancellationToken);

        var existing = await context.ActivityCategoryBudgets
            .Where(b => b.ActivityId == activityId)
            .ToListAsync(cancellationToken);
        context.ActivityCategoryBudgets.RemoveRange(existing);
        context.ActivityCategoryBudgets.AddRange(template.Select(t => new ActivityCategoryBudget
        {
            ActivityId = activityId,
            ExpenseCategoryId = t.Id,
            Amount = t.Budget
        }));

        await context.SaveChangesAsync(cancellationToken);
        return template.Count;
    }
}
