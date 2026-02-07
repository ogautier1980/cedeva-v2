using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cedeva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDisplayOrderAndIsActiveToActivityQuestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "ActivityQuestions",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ActivityQuestions",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // Initialize DisplayOrder with sequential values per Activity
            migrationBuilder.Sql(@"
                WITH OrderedQuestions AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (PARTITION BY ActivityId ORDER BY Id) AS NewOrder
                    FROM ActivityQuestions
                )
                UPDATE ActivityQuestions
                SET DisplayOrder = oq.NewOrder
                FROM ActivityQuestions aq
                INNER JOIN OrderedQuestions oq ON aq.Id = oq.Id;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "ActivityQuestions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ActivityQuestions");
        }
    }
}
