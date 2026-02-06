using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cedeva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AssignConfirmedBookingsWithoutGroupToDefaultGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // For each activity that has confirmed bookings without a group,
            // create a "Sans groupe" group and assign those bookings to it
            migrationBuilder.Sql(@"
                -- Step 1: Create 'Sans groupe' groups for activities that have confirmed bookings without a group
                INSERT INTO ActivityGroups (ActivityId, Label, Capacity, CreatedAt, CreatedBy)
                SELECT DISTINCT
                    b.ActivityId,
                    'Sans groupe',
                    NULL,
                    GETUTCDATE(),
                    'System'
                FROM Bookings b
                WHERE b.IsConfirmed = 1
                    AND b.GroupId IS NULL
                    AND NOT EXISTS (
                        SELECT 1
                        FROM ActivityGroups ag
                        WHERE ag.ActivityId = b.ActivityId
                            AND ag.Label = 'Sans groupe'
                    );

                -- Step 2: Assign confirmed bookings without a group to the 'Sans groupe' group
                UPDATE b
                SET b.GroupId = ag.Id
                FROM Bookings b
                INNER JOIN ActivityGroups ag ON ag.ActivityId = b.ActivityId AND ag.Label = 'Sans groupe'
                WHERE b.IsConfirmed = 1
                    AND b.GroupId IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
