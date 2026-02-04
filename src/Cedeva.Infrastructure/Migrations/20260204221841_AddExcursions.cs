using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cedeva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExcursions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExcursionId",
                table: "Expenses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Excursions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExcursionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Cost = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ActivityId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Excursions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Excursions_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExcursionGroups",
                columns: table => new
                {
                    ExcursionId = table.Column<int>(type: "int", nullable: false),
                    ActivityGroupId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExcursionGroups", x => new { x.ExcursionId, x.ActivityGroupId });
                    table.ForeignKey(
                        name: "FK_ExcursionGroups_ActivityGroups_ActivityGroupId",
                        column: x => x.ActivityGroupId,
                        principalTable: "ActivityGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExcursionGroups_Excursions_ExcursionId",
                        column: x => x.ExcursionId,
                        principalTable: "Excursions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExcursionRegistrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExcursionId = table.Column<int>(type: "int", nullable: false),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    RegistrationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsPresent = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExcursionRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExcursionRegistrations_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExcursionRegistrations_Excursions_ExcursionId",
                        column: x => x.ExcursionId,
                        principalTable: "Excursions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ExcursionId",
                table: "Expenses",
                column: "ExcursionId");

            migrationBuilder.CreateIndex(
                name: "IX_ExcursionGroups_ActivityGroupId",
                table: "ExcursionGroups",
                column: "ActivityGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ExcursionRegistrations_BookingId",
                table: "ExcursionRegistrations",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_ExcursionRegistrations_ExcursionId_BookingId",
                table: "ExcursionRegistrations",
                columns: new[] { "ExcursionId", "BookingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Excursions_ActivityId",
                table: "Excursions",
                column: "ActivityId");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Excursions_ExcursionId",
                table: "Expenses",
                column: "ExcursionId",
                principalTable: "Excursions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Excursions_ExcursionId",
                table: "Expenses");

            migrationBuilder.DropTable(
                name: "ExcursionGroups");

            migrationBuilder.DropTable(
                name: "ExcursionRegistrations");

            migrationBuilder.DropTable(
                name: "Excursions");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_ExcursionId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "ExcursionId",
                table: "Expenses");
        }
    }
}
