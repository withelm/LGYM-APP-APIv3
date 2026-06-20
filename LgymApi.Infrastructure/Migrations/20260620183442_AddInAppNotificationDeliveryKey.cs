using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInAppNotificationDeliveryKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryKey",
                table: "in_app_notifications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_in_app_notifications_RecipientId_Type_DeliveryKey",
                table: "in_app_notifications",
                columns: new[] { "RecipientId", "Type", "DeliveryKey" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE AND \"DeliveryKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_in_app_notifications_RecipientId_Type_DeliveryKey",
                table: "in_app_notifications");

            migrationBuilder.DropColumn(
                name: "DeliveryKey",
                table: "in_app_notifications");
        }
    }
}
