using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cedeva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseCategoryTypeAndTicketNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TicketNumber",
                table: "Payments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ExpenseCategoryId",
                table: "Expenses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TicketNumber",
                table: "Expenses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CategoryType",
                table: "ExpenseCategories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // IsIncome (bool) -> CategoryType (0=Expense, 1=Income, 2=OffBalance): backfill before dropping the old column.
            migrationBuilder.Sql(
                "UPDATE \"ExpenseCategories\" SET \"CategoryType\" = CASE WHEN \"IsIncome\" THEN 1 ELSE 0 END;");

            migrationBuilder.DropColumn(
                name: "IsIncome",
                table: "ExpenseCategories");

            // Match existing free-text Expense.Category to the curated ExpenseCategory of the same
            // organisation (by name), so pre-existing expenses immediately benefit from the new FK
            // (Hors bilan exclusion, category report) without manual re-entry.
            migrationBuilder.Sql(
                """
                UPDATE "Expenses" e
                SET "ExpenseCategoryId" = ec."Id"
                FROM "ExpenseCategories" ec, "Activities" a
                WHERE a."Id" = e."ActivityId"
                  AND ec."OrganisationId" = a."OrganisationId"
                  AND ec."Name" = e."Category"
                  AND e."ExpenseCategoryId" IS NULL;
                """);

            // Backfill TicketNumber for pre-existing Payments/Expenses: one sequence per activity,
            // shared between the two tables, ordered chronologically (oldest = ticket #1).
            migrationBuilder.Sql(
                """
                WITH combined AS (
                    SELECT 'Payment' AS entity, p."Id" AS id, b."ActivityId" AS activity_id, p."PaymentDate" AS txn_date, p."CreatedAt" AS created_at
                    FROM "Payments" p
                    JOIN "Bookings" b ON b."Id" = p."BookingId"
                    UNION ALL
                    SELECT 'Expense' AS entity, e."Id" AS id, e."ActivityId" AS activity_id, e."ExpenseDate" AS txn_date, e."CreatedAt" AS created_at
                    FROM "Expenses" e
                ),
                ranked AS (
                    SELECT entity, id,
                           ROW_NUMBER() OVER (PARTITION BY activity_id ORDER BY txn_date, created_at, id) AS rn
                    FROM combined
                )
                UPDATE "Payments" p
                SET "TicketNumber" = r.rn
                FROM ranked r
                WHERE r.entity = 'Payment' AND r.id = p."Id";
                """);

            migrationBuilder.Sql(
                """
                WITH combined AS (
                    SELECT 'Payment' AS entity, p."Id" AS id, b."ActivityId" AS activity_id, p."PaymentDate" AS txn_date, p."CreatedAt" AS created_at
                    FROM "Payments" p
                    JOIN "Bookings" b ON b."Id" = p."BookingId"
                    UNION ALL
                    SELECT 'Expense' AS entity, e."Id" AS id, e."ActivityId" AS activity_id, e."ExpenseDate" AS txn_date, e."CreatedAt" AS created_at
                    FROM "Expenses" e
                ),
                ranked AS (
                    SELECT entity, id,
                           ROW_NUMBER() OVER (PARTITION BY activity_id ORDER BY txn_date, created_at, id) AS rn
                    FROM combined
                )
                UPDATE "Expenses" e
                SET "TicketNumber" = r.rn
                FROM ranked r
                WHERE r.entity = 'Expense' AND r.id = e."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ExpenseCategoryId",
                table: "Expenses",
                column: "ExpenseCategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_ExpenseCategories_ExpenseCategoryId",
                table: "Expenses",
                column: "ExpenseCategoryId",
                principalTable: "ExpenseCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_ExpenseCategories_ExpenseCategoryId",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_ExpenseCategoryId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "TicketNumber",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ExpenseCategoryId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "TicketNumber",
                table: "Expenses");

            migrationBuilder.AddColumn<bool>(
                name: "IsIncome",
                table: "ExpenseCategories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "UPDATE \"ExpenseCategories\" SET \"IsIncome\" = (\"CategoryType\" = 1);");

            migrationBuilder.DropColumn(
                name: "CategoryType",
                table: "ExpenseCategories");
        }
    }
}
