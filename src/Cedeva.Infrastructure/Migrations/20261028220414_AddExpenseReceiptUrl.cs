using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cedeva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseReceiptUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReceiptUrl",
                table: "Expenses",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiptUrl",
                table: "Expenses");
        }
    }
}
