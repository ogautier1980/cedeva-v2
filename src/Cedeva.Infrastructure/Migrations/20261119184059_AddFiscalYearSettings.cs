using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cedeva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscalYearSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FiscalYearSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    MaxDailyAmount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    TaxCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalYearSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "FiscalYearSettings",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "MaxDailyAmount", "ModifiedAt", "ModifiedBy", "TaxCode", "Year" },
                values: new object[] { 1, new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Utc), "System", 16.90m, null, null, "1384", 2025 });

            // The seeded row has an explicit Id: move the identity sequence past it, or the admin's
            // first added year would get Id 1 again and fail on the primary key.
            migrationBuilder.Sql("""SELECT setval(pg_get_serial_sequence('"FiscalYearSettings"', 'Id'), (SELECT MAX("Id") FROM "FiscalYearSettings"));""");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYearSettings_Year",
                table: "FiscalYearSettings",
                column: "Year",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FiscalYearSettings");
        }
    }
}
