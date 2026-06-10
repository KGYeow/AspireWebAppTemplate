using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebAppTemplate.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "ApplicationRoles",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ApplicationRoles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "ApplicationRoles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ApplicationRoles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedUtc",
                table: "ApplicationRoles",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "ApplicationRoles");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ApplicationRoles");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "ApplicationRoles");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ApplicationRoles");

            migrationBuilder.DropColumn(
                name: "UpdatedUtc",
                table: "ApplicationRoles");
        }
    }
}
