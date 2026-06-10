using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebAppTemplate.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleSystemFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "ApplicationRoles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                table: "ApplicationRoles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Position",
                table: "ApplicationRoles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresMinimumUser",
                table: "ApplicationRoles",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "ApplicationRoles");

            migrationBuilder.DropColumn(
                name: "IsSystem",
                table: "ApplicationRoles");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "ApplicationRoles");

            migrationBuilder.DropColumn(
                name: "RequiresMinimumUser",
                table: "ApplicationRoles");
        }
    }
}
