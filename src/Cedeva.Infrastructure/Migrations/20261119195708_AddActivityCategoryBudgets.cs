using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cedeva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityCategoryBudgets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityCategoryBudgets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActivityId = table.Column<int>(type: "integer", nullable: false),
                    ExpenseCategoryId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityCategoryBudgets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityCategoryBudgets_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActivityCategoryBudgets_ExpenseCategories_ExpenseCategoryId",
                        column: x => x.ExpenseCategoryId,
                        principalTable: "ExpenseCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityCategoryBudgets_ActivityId_ExpenseCategoryId",
                table: "ActivityCategoryBudgets",
                columns: new[] { "ActivityId", "ExpenseCategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityCategoryBudgets_ExpenseCategoryId",
                table: "ActivityCategoryBudgets",
                column: "ExpenseCategoryId");

            // Until now every activity's BILAN showed the organisation's category budgets: give each
            // existing activity a copy of them, as a newly created activity gets (Hors bilan excluded).
            migrationBuilder.Sql("""
                INSERT INTO "ActivityCategoryBudgets" ("ActivityId", "ExpenseCategoryId", "Amount", "CreatedAt", "CreatedBy")
                SELECT a."Id", c."Id", c."Budget", now(), 'System'
                FROM "Activities" a
                JOIN "ExpenseCategories" c ON c."OrganisationId" = a."OrganisationId"
                WHERE c."Budget" IS NOT NULL AND c."CategoryType" <> 2;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityCategoryBudgets");
        }
    }
}
