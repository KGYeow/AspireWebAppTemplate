using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AspireWebAppTemplate.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationPopupsEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NotificationPopupsEnabled",
                table: "ApplicationUsers",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // Set existing users to true (popups enabled by default).
            migrationBuilder.Sql("UPDATE [ApplicationUsers] SET [NotificationPopupsEnabled] = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotificationPopupsEnabled",
                table: "ApplicationUsers");
        }
    }
}
