using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cedeva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityAllowDayByDayRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowDayByDayRegistration",
                table: "Activities",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowDayByDayRegistration",
                table: "Activities");
        }
    }
}
