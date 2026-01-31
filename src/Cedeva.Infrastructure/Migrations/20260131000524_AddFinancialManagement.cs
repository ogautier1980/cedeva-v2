using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cedeva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankAccountName",
                table: "Organisations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankAccountNumber",
                table: "Organisations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExpenseType",
                table: "Expenses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "Bookings",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PaymentStatus",
                table: "Bookings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StructuredCommunication",
                table: "Bookings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                table: "Bookings",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "CodaFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganisationId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ImportDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StatementDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: false),
                    OldBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NewBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TransactionCount = table.Column<int>(type: "int", nullable: false),
                    ImportedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodaFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CodaFiles_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganisationId = table.Column<int>(type: "int", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    StructuredCommunication = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FreeCommunication = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CounterpartyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CounterpartyAccount = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: true),
                    TransactionCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CodaFileId = table.Column<int>(type: "int", nullable: false),
                    IsReconciled = table.Column<bool>(type: "bit", nullable: false),
                    PaymentId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankTransactions_CodaFiles_CodaFileId",
                        column: x => x.CodaFileId,
                        principalTable: "CodaFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BankTransactions_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StructuredCommunication = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BankTransactionId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_BankTransactions_BankTransactionId",
                        column: x => x.BankTransactionId,
                        principalTable: "BankTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActivityFinancialTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActivityId = table.Column<int>(type: "int", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PaymentId = table.Column<int>(type: "int", nullable: true),
                    ExpenseId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityFinancialTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityFinancialTransactions_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityFinancialTransactions_Expenses_ExpenseId",
                        column: x => x.ExpenseId,
                        principalTable: "Expenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityFinancialTransactions_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_StructuredCommunication",
                table: "Bookings",
                column: "StructuredCommunication",
                unique: true,
                filter: "[StructuredCommunication] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityFinancialTransactions_ActivityId",
                table: "ActivityFinancialTransactions",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityFinancialTransactions_ExpenseId",
                table: "ActivityFinancialTransactions",
                column: "ExpenseId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityFinancialTransactions_PaymentId",
                table: "ActivityFinancialTransactions",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityFinancialTransactions_TransactionDate",
                table: "ActivityFinancialTransactions",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_CodaFileId",
                table: "BankTransactions",
                column: "CodaFileId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_IsReconciled",
                table: "BankTransactions",
                column: "IsReconciled");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_OrganisationId",
                table: "BankTransactions",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_StructuredCommunication",
                table: "BankTransactions",
                column: "StructuredCommunication");

            migrationBuilder.CreateIndex(
                name: "IX_CodaFiles_ImportDate",
                table: "CodaFiles",
                column: "ImportDate");

            migrationBuilder.CreateIndex(
                name: "IX_CodaFiles_OrganisationId",
                table: "CodaFiles",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_BankTransactionId",
                table: "Payments",
                column: "BankTransactionId",
                unique: true,
                filter: "[BankTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_BookingId",
                table: "Payments",
                column: "BookingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityFinancialTransactions");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "BankTransactions");

            migrationBuilder.DropTable(
                name: "CodaFiles");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_StructuredCommunication",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "BankAccountName",
                table: "Organisations");

            migrationBuilder.DropColumn(
                name: "BankAccountNumber",
                table: "Organisations");

            migrationBuilder.DropColumn(
                name: "ExpenseType",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "StructuredCommunication",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                table: "Bookings");
        }
    }
}
