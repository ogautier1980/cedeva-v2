using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cedeva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailTemplateActivityScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsShared",
                table: "EmailTemplates");

            migrationBuilder.AddColumn<int>(
                name: "ActivityId",
                table: "EmailTemplates",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_ActivityId",
                table: "EmailTemplates",
                column: "ActivityId");

            migrationBuilder.AddForeignKey(
                name: "FK_EmailTemplates_Activities_ActivityId",
                table: "EmailTemplates",
                column: "ActivityId",
                principalTable: "Activities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmailTemplates_Activities_ActivityId",
                table: "EmailTemplates");

            migrationBuilder.DropIndex(
                name: "IX_EmailTemplates_ActivityId",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "ActivityId",
                table: "EmailTemplates");

            migrationBuilder.AddColumn<bool>(
                name: "IsShared",
                table: "EmailTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
