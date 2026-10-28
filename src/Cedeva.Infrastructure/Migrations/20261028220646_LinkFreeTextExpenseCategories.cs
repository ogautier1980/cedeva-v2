using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cedeva.Infrastructure.Migrations
{
    /// <summary>
    /// Data-only: expenses created while the category was a free-text field may carry a category name
    /// with no ExpenseCategory behind it, so the BILAN files them under "Sans catégorie". Each such
    /// expense is linked to its organisation's category of the same name (ignoring case and accents,
    /// e.g. "Materiel" → "Matériel"); names still unmatched become new Dépense categories, which the
    /// expenses are then linked to. Down is a no-op: the created categories are ordinary categories
    /// the coordinator may already have edited, budgeted or reused.
    /// </summary>
    public partial class LinkFreeTextExpenseCategories : Migration
    {
        // Case- and accent-insensitive key for a category name (no unaccent extension needed).
        private static string Normalized(string column) =>
            $"translate(lower(trim({column})), 'àâäáãéèêëíìîïóòôöõúùûüçñ', 'aaaaaeeeeiiiiooooouuuucn')";

        private static readonly string LinkToExistingCategories = $"""
            UPDATE "Expenses" e
            SET "ExpenseCategoryId" = c."Id"
            FROM "Activities" a, "ExpenseCategories" c
            WHERE e."ActivityId" = a."Id"
              AND c."OrganisationId" = a."OrganisationId"
              AND e."ExpenseCategoryId" IS NULL
              AND coalesce(trim(e."Category"), '') <> ''
              AND {Normalized("c.\"Name\"")} = {Normalized("e.\"Category\"")};
            """;

        private static readonly string CreateMissingCategories = $"""
            INSERT INTO "ExpenseCategories" ("OrganisationId", "Name", "CategoryType", "CreatedAt", "CreatedBy")
            SELECT DISTINCT ON (a."OrganisationId", {Normalized("e.\"Category\"")})
                   a."OrganisationId", left(trim(e."Category"), 50), 0, now(), 'System'
            FROM "Expenses" e
            JOIN "Activities" a ON a."Id" = e."ActivityId"
            WHERE e."ExpenseCategoryId" IS NULL
              AND coalesce(trim(e."Category"), '') <> ''
            ORDER BY a."OrganisationId", {Normalized("e.\"Category\"")}, trim(e."Category");
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(LinkToExistingCategories);
            migrationBuilder.Sql(CreateMissingCategories);
            // Link the expenses to the categories just created.
            migrationBuilder.Sql(LinkToExistingCategories);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
