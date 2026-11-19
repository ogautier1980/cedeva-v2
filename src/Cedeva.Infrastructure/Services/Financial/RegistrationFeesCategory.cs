using System.Text;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Infrastructure.Services.Financial;

/// <summary>
/// The organisation's P.A.F. category: an Entrée that, besides lines encoded by hand, automatically
/// includes every registration payment (BILAN, transactions list). Organisations are created in
/// several places (admin, seeders, CSV import), so the category is ensured where categories are read.
/// </summary>
public static class RegistrationFeesCategory
{
    public const string DefaultName = "P.A.F.";

    /// <summary>
    /// Returns the organisation's P.A.F. category, creating it — or adopting an existing category
    /// already named "P.A.F." / "PAF" — the first time. Query filters are bypassed: the caller has
    /// already resolved the organisation.
    /// </summary>
    public static async Task<ExpenseCategory> EnsureAsync(CedevaDbContext context, int organisationId, CancellationToken cancellationToken = default)
    {
        var categories = await context.ExpenseCategories.IgnoreQueryFilters()
            .Where(c => c.OrganisationId == organisationId)
            .ToListAsync(cancellationToken);

        var category = categories.FirstOrDefault(c => c.IsRegistrationFees);
        if (category != null)
        {
            return category;
        }

        category = categories.FirstOrDefault(c => IsPafName(c.Name));
        if (category == null)
        {
            category = new ExpenseCategory { OrganisationId = organisationId, Name = DefaultName };
            context.ExpenseCategories.Add(category);
        }

        category.IsRegistrationFees = true;
        category.CategoryType = ExpenseCategoryType.Income;
        await context.SaveChangesAsync(cancellationToken);
        return category;
    }

    // "P.A.F.", "PAF", "p a f" … — letters only, case-insensitive.
    private static bool IsPafName(string name)
    {
        var letters = new StringBuilder();
        foreach (var ch in name)
        {
            if (char.IsLetter(ch)) letters.Append(char.ToLowerInvariant(ch));
        }
        return letters.ToString() == "paf";
    }
}
