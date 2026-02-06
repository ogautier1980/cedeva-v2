using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cedeva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditFieldsToAllEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmailTemplates_AspNetUsers_CreatedByUserId",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Payments");

            migrationBuilder.RenameColumn(
                name: "LastModifiedDate",
                table: "EmailTemplates",
                newName: "ModifiedAt");

            migrationBuilder.RenameColumn(
                name: "CreatedDate",
                table: "EmailTemplates",
                newName: "CreatedAt");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "TeamMembers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "TeamMembers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "TeamMembers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "TeamMembers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Payments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Payments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "Payments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Payments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Parents",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Parents",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "Parents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Parents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Organisations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Organisations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "Organisations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Organisations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Expenses",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Expenses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "Expenses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Expenses",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ExcursionTeamMembers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ExcursionTeamMembers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "ExcursionTeamMembers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ExcursionTeamMembers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Excursions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Excursions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "Excursions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Excursions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ExcursionRegistrations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ExcursionRegistrations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "ExcursionRegistrations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ExcursionRegistrations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ExcursionGroups",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ExcursionGroups",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "ExcursionGroups",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ExcursionGroups",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedByUserId",
                table: "EmailTemplates",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "EmailTemplates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "EmailTemplates",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "EmailsSent",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "EmailsSent",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "EmailsSent",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "EmailsSent",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "CodaFiles",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "CodaFiles",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "CodaFiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "CodaFiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Children",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Children",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "Children",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Children",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Bookings",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Bookings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "Bookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Bookings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "BookingDays",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "BookingDays",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "BookingDays",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "BookingDays",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "BankTransactions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "BankTransactions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "BankTransactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "BankTransactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Addresses",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "Addresses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ActivityQuestions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ActivityQuestions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "ActivityQuestions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ActivityQuestions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ActivityQuestionAnswers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ActivityQuestionAnswers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "ActivityQuestionAnswers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ActivityQuestionAnswers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ActivityGroups",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ActivityGroups",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "ActivityGroups",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ActivityGroups",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ActivityFinancialTransactions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ActivityFinancialTransactions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "ActivityFinancialTransactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ActivityFinancialTransactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ActivityDays",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ActivityDays",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "ActivityDays",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ActivityDays",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Activities",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Activities",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "Activities",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Activities",
                type: "nvarchar(max)",
                nullable: true);

            // Initialize audit fields for existing data
            migrationBuilder.Sql(@"
                -- Initialize all entities with default audit values
                UPDATE Activities SET CreatedAt = GETUTCDATE(), CreatedBy = 'System' WHERE CreatedBy = '';
                UPDATE ActivityDays SET CreatedAt = GETUTCDATE(), CreatedBy = 'System' WHERE CreatedBy = '';
                UPDATE ActivityGroups SET CreatedAt = GETUTCDATE(), CreatedBy = 'System' WHERE CreatedBy = '';
                UPDATE ActivityQuestions SET CreatedAt = GETUTCDATE(), CreatedBy = 'System' WHERE CreatedBy = '';
                UPDATE ActivityQuestionAnswers SET CreatedAt = GETUTCDATE(), CreatedBy = 'System' WHERE CreatedBy = '';
                UPDATE ActivityFinancialTransactions SET CreatedAt = GETUTCDATE(), CreatedBy = 'System' WHERE CreatedBy = '';
                UPDATE Addresses SET CreatedAt = GETUTCDATE(), CreatedBy = 'System' WHERE CreatedBy = '';
                UPDATE BankTransactions SET CreatedAt = GETUTCDATE(), CreatedBy = 'System' WHERE CreatedBy = '';
                UPDATE Bookings SET CreatedAt = GETUTCDATE(), CreatedBy = 'System' WHERE CreatedBy = '';
                UPDATE BookingDays SET CreatedAt = GETUTCDATE(), CreatedBy = 'System' WHERE CreatedBy = '';
                UPDATE Children SET CreatedAt = GETUTCDATE(), CreatedBy = 'System' WHERE CreatedBy = '';
                UPDATE CodaFiles SET CreatedAt = GETUTCDATE(), CreatedBy = 'System' WHERE CreatedBy = '';
                UPDATE EmailsSent SET CreatedAt = GETUTCDATE(), CreatedBy = 'System' WHERE CreatedBy = '';
                UPDATE Excursions SET CreatedAt = GETUTCDATE(), CreatedBy = 'System' WHERE CreatedBy = '';
                UPDATE ExcursionGroups SET CreatedAt = GETUTCDATE(), CreatedBy = 'System' WHERE CreatedBy = '';
                UPDATE ExcursionRegistrations SET CreatedAt = GETUTCDATE(), CreatedBy = 'System' WHERE CreatedBy = '';
                UPDATE ExcursionTeamMembers SET CreatedAt = GETUTCDATE(), CreatedBy = 'System' WHERE CreatedBy = '';
                UPDATE Expenses SET CreatedAt = GETUTCDATE(), CreatedBy = 'System' WHERE CreatedBy = '';
                UPDATE Organisations SET CreatedAt = GETUTCDATE(), CreatedBy = 'System' WHERE CreatedBy = '';
                UPDATE Parents SET CreatedAt = GETUTCDATE(), CreatedBy = 'System' WHERE CreatedBy = '';
                UPDATE Payments SET CreatedAt = GETUTCDATE(), CreatedBy = 'System' WHERE CreatedBy = '';
                UPDATE TeamMembers SET CreatedAt = GETUTCDATE(), CreatedBy = 'System' WHERE CreatedBy = '';
                UPDATE AspNetUsers SET CreatedAt = GETUTCDATE(), CreatedBy = 'System' WHERE CreatedBy = '';

                -- EmailTemplate special case: copy data from old columns to new audit columns
                UPDATE EmailTemplates
                SET CreatedBy = ISNULL(CreatedByUserId, 'System')
                WHERE CreatedBy = '';
            ");

            migrationBuilder.AddForeignKey(
                name: "FK_EmailTemplates_AspNetUsers_CreatedByUserId",
                table: "EmailTemplates",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmailTemplates_AspNetUsers_CreatedByUserId",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "TeamMembers");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "TeamMembers");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "TeamMembers");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "TeamMembers");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Parents");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Parents");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "Parents");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Parents");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Organisations");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Organisations");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "Organisations");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Organisations");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ExcursionTeamMembers");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ExcursionTeamMembers");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "ExcursionTeamMembers");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ExcursionTeamMembers");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Excursions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Excursions");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "Excursions");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Excursions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ExcursionRegistrations");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ExcursionRegistrations");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "ExcursionRegistrations");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ExcursionRegistrations");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ExcursionGroups");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ExcursionGroups");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "ExcursionGroups");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ExcursionGroups");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "EmailsSent");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "EmailsSent");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "EmailsSent");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "EmailsSent");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "CodaFiles");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "CodaFiles");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "CodaFiles");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "CodaFiles");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Children");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Children");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "Children");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Children");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "BookingDays");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "BookingDays");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "BookingDays");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "BookingDays");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ActivityQuestions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ActivityQuestions");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "ActivityQuestions");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ActivityQuestions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ActivityQuestionAnswers");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ActivityQuestionAnswers");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "ActivityQuestionAnswers");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ActivityQuestionAnswers");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ActivityGroups");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ActivityGroups");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "ActivityGroups");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ActivityGroups");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ActivityFinancialTransactions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ActivityFinancialTransactions");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "ActivityFinancialTransactions");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ActivityFinancialTransactions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ActivityDays");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ActivityDays");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "ActivityDays");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ActivityDays");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Activities");

            migrationBuilder.RenameColumn(
                name: "ModifiedAt",
                table: "EmailTemplates",
                newName: "LastModifiedDate");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "EmailTemplates",
                newName: "CreatedDate");

            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "Payments",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedByUserId",
                table: "EmailTemplates",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EmailTemplates_AspNetUsers_CreatedByUserId",
                table: "EmailTemplates",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
