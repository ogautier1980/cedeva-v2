using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cedeva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailTemplatesAndUpdateEmailSent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "EmailsSent",
                type: "nvarchar(max)",
                maxLength: 5000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1024)",
                oldMaxLength: 1024);

            migrationBuilder.AddColumn<int>(
                name: "ScheduledDayId",
                table: "EmailsSent",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SendSeparateEmailPerChild",
                table: "EmailsSent",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "EmailTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganisationId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TemplateType = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    HtmlContent = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsShared = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailTemplates_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmailTemplates_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailsSent_ScheduledDayId",
                table: "EmailsSent",
                column: "ScheduledDayId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_CreatedByUserId",
                table: "EmailTemplates",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_OrganisationId",
                table: "EmailTemplates",
                column: "OrganisationId");

            migrationBuilder.AddForeignKey(
                name: "FK_EmailsSent_ActivityDays_ScheduledDayId",
                table: "EmailsSent",
                column: "ScheduledDayId",
                principalTable: "ActivityDays",
                principalColumn: "DayId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmailsSent_ActivityDays_ScheduledDayId",
                table: "EmailsSent");

            migrationBuilder.DropTable(
                name: "EmailTemplates");

            migrationBuilder.DropIndex(
                name: "IX_EmailsSent_ScheduledDayId",
                table: "EmailsSent");

            migrationBuilder.DropColumn(
                name: "ScheduledDayId",
                table: "EmailsSent");

            migrationBuilder.DropColumn(
                name: "SendSeparateEmailPerChild",
                table: "EmailsSent");

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "EmailsSent",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldMaxLength: 5000);
        }
    }
}
