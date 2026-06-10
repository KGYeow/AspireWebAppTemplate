using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebAppTemplate.Migrations
{
    /// <inheritdoc />
    public partial class AddDateTimeFormatAndTheme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DateTimeFormat",
                table: "ApplicationUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Theme",
                table: "ApplicationUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateTimeFormat",
                table: "ApplicationUsers");

            migrationBuilder.DropColumn(
                name: "Theme",
                table: "ApplicationUsers");
        }
    }
}
