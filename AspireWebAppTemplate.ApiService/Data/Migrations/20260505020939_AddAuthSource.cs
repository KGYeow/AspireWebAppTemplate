using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebAppTemplate.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthSource",
                table: "ApplicationUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthSource",
                table: "ApplicationUsers");
        }
    }
}
