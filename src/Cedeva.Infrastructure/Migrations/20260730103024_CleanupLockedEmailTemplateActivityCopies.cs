using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cedeva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CleanupLockedEmailTemplateActivityCopies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data-only cleanup (Lot E, decision 2026-07-30): "Confirmation d'inscription" (1),
            // "Rappel fiche médicale" (3) and "Rappel paiement" (4) become unique, organisation-wide
            // templates — no per-activity copies. Every activity created since the Lot 4 auto-copy
            // shipped may have its own copy of these 3 types; remove them so all activities fall back
            // to the single org-level template via EmailTemplateService.GetDefaultTemplateAsync's
            // existing activity->org fallback. Irreversible by design (see Down): a coordinator who
            // had customized one of these 3 templates for a specific activity loses that customization,
            // per the explicit product decision to prioritize "unique and shared" over preserving
            // already-shipped per-activity divergence.
            migrationBuilder.Sql(
                "DELETE FROM EmailTemplates WHERE ActivityId IS NOT NULL AND TemplateType IN (1, 3, 4);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally irreversible: the deleted per-activity rows (and whatever customizations
            // they held) cannot be reconstructed from the org-level template alone.
        }
    }
}
