using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cedeva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamMemberDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeamMemberDays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActivityDayId = table.Column<int>(type: "int", nullable: false),
                    TeamMemberId = table.Column<int>(type: "int", nullable: false),
                    IsPresent = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMemberDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamMemberDays_ActivityDays_ActivityDayId",
                        column: x => x.ActivityDayId,
                        principalTable: "ActivityDays",
                        principalColumn: "DayId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamMemberDays_TeamMembers_TeamMemberId",
                        column: x => x.TeamMemberId,
                        principalTable: "TeamMembers",
                        principalColumn: "TeamMemberId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamMemberDays_ActivityDayId",
                table: "TeamMemberDays",
                column: "ActivityDayId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMemberDays_TeamMemberId",
                table: "TeamMemberDays",
                column: "TeamMemberId");

            // Backfill (Lot G, 2026-07-30): salary calculation used to assume every assigned team
            // member worked every day of the activity (activity.Days.Count * DailyCompensation).
            // Now that it multiplies by each member's actual TeamMemberDays.Count(IsPresent) instead,
            // every existing activity/team-member assignment needs a row per day defaulting to
            // present=true so already-computed salaries for in-progress/past activities don't change
            // retroactively - a coordinator only affects the total by actively unchecking a day.
            migrationBuilder.Sql(
                @"
                INSERT INTO TeamMemberDays (ActivityDayId, TeamMemberId, IsPresent, CreatedAt, CreatedBy)
                SELECT ad.DayId, atm.TeamMembersTeamMemberId, 1, GETUTCDATE(), 'System'
                FROM ActivityDays ad
                JOIN ActivityTeamMembers atm ON atm.ActivitiesId = ad.ActivityId
                WHERE NOT EXISTS (
                    SELECT 1 FROM TeamMemberDays tmd
                    WHERE tmd.ActivityDayId = ad.DayId AND tmd.TeamMemberId = atm.TeamMembersTeamMemberId
                );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamMemberDays");
        }
    }
}
