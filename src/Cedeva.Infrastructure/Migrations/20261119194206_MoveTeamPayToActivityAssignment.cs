using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cedeva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveTeamPayToActivityAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Thomas (19/11/2026): status and daily rate change from one activity to the next — they
            // move from the member sheet to the activity assignment. Copy before dropping.
            migrationBuilder.AddColumn<decimal>(
                name: "DailyCompensation",
                table: "ActivityTeamMembers",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "ActivityTeamMembers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE "ActivityTeamMembers" atm
                SET "DailyCompensation" = tm."DailyCompensation",
                    "Status" = tm."Status"
                FROM "TeamMembers" tm
                WHERE tm."TeamMemberId" = atm."TeamMembersTeamMemberId";
                """);

            migrationBuilder.DropColumn(
                name: "DailyCompensation",
                table: "TeamMembers");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "TeamMembers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DailyCompensation",
                table: "TeamMembers",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "TeamMembers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Back to one value per member: the one of their most recent activity.
            migrationBuilder.Sql("""
                UPDATE "TeamMembers" tm
                SET "DailyCompensation" = latest."DailyCompensation",
                    "Status" = latest."Status"
                FROM (
                    SELECT DISTINCT ON (atm."TeamMembersTeamMemberId")
                           atm."TeamMembersTeamMemberId", atm."DailyCompensation", atm."Status"
                    FROM "ActivityTeamMembers" atm
                    JOIN "Activities" a ON a."Id" = atm."ActivitiesId"
                    ORDER BY atm."TeamMembersTeamMemberId", a."StartDate" DESC
                ) latest
                WHERE latest."TeamMembersTeamMemberId" = tm."TeamMemberId";
                """);

            migrationBuilder.DropColumn(
                name: "DailyCompensation",
                table: "ActivityTeamMembers");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ActivityTeamMembers");
        }
    }
}
