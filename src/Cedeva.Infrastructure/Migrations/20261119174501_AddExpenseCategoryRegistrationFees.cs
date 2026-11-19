using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cedeva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseCategoryRegistrationFees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRegistrationFees",
                table: "ExpenseCategories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_OrganisationId_RegistrationFees",
                table: "ExpenseCategories",
                column: "OrganisationId",
                unique: true,
                filter: "\"IsRegistrationFees\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExpenseCategories_OrganisationId_RegistrationFees",
                table: "ExpenseCategories");

            migrationBuilder.DropColumn(
                name: "IsRegistrationFees",
                table: "ExpenseCategories");
        }
    }
}
