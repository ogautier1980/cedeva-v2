using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cedeva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixExpenseActivityDeleteBehavior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Activities_ActivityId",
                table: "Expenses");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Activities_ActivityId",
                table: "Expenses",
                column: "ActivityId",
                principalTable: "Activities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Activities_ActivityId",
                table: "Expenses");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Activities_ActivityId",
                table: "Expenses",
                column: "ActivityId",
                principalTable: "Activities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
