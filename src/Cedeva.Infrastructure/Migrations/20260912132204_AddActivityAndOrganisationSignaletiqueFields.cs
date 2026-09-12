using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cedeva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityAndOrganisationSignaletiqueFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyNumber",
                table: "Organisations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone1",
                table: "Organisations",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone2",
                table: "Organisations",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponsibleSignatureUrl",
                table: "Organisations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AddressId",
                table: "Activities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankAccountNumber",
                table: "Activities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyNumber",
                table: "Activities",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayTitle",
                table: "Activities",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Activities",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "Activities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone1",
                table: "Activities",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone2",
                table: "Activities",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponsibleName",
                table: "Activities",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponsibleSignatureUrl",
                table: "Activities",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Activities_AddressId",
                table: "Activities",
                column: "AddressId");

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_Addresses_AddressId",
                table: "Activities",
                column: "AddressId",
                principalTable: "Addresses",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activities_Addresses_AddressId",
                table: "Activities");

            migrationBuilder.DropIndex(
                name: "IX_Activities_AddressId",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "CompanyNumber",
                table: "Organisations");

            migrationBuilder.DropColumn(
                name: "Phone1",
                table: "Organisations");

            migrationBuilder.DropColumn(
                name: "Phone2",
                table: "Organisations");

            migrationBuilder.DropColumn(
                name: "ResponsibleSignatureUrl",
                table: "Organisations");

            migrationBuilder.DropColumn(
                name: "AddressId",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "BankAccountNumber",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "CompanyNumber",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "DisplayTitle",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "Phone1",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "Phone2",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "ResponsibleName",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "ResponsibleSignatureUrl",
                table: "Activities");
        }
    }
}
