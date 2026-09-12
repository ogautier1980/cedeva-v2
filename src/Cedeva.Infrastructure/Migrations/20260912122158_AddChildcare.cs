using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cedeva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChildcare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ChildcarePricePerDay",
                table: "Activities",
                type: "numeric",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChildcareRegistrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    ActivityDayId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChildcareRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChildcareRegistrations_ActivityDays_ActivityDayId",
                        column: x => x.ActivityDayId,
                        principalTable: "ActivityDays",
                        principalColumn: "DayId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChildcareRegistrations_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChildcareRegistrations_ActivityDayId",
                table: "ChildcareRegistrations",
                column: "ActivityDayId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildcareRegistrations_BookingId_ActivityDayId",
                table: "ChildcareRegistrations",
                columns: new[] { "BookingId", "ActivityDayId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChildcareRegistrations");

            migrationBuilder.DropColumn(
                name: "ChildcarePricePerDay",
                table: "Activities");
        }
    }
}
