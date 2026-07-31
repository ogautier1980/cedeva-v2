using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cedeva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityWizardFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FullMessage",
                table: "Activities",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxChildrenPerDay",
                table: "Activities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NoActiveFormMessage",
                table: "Activities",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCodeErrorMessage",
                table: "Activities",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublicationEndDate",
                table: "Activities",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublicationStartDate",
                table: "Activities",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RedirectUrlAfterSubmit",
                table: "Activities",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegulationAcceptanceText",
                table: "Activities",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegulationLinkUrl",
                table: "Activities",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FullMessage",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "MaxChildrenPerDay",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "NoActiveFormMessage",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "PostalCodeErrorMessage",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "PublicationEndDate",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "PublicationStartDate",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "RedirectUrlAfterSubmit",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "RegulationAcceptanceText",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "RegulationLinkUrl",
                table: "Activities");
        }
    }
}
