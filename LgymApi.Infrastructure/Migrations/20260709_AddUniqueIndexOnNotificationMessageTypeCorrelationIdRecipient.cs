using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    public partial class AddUniqueIndexOnNotificationMessageTypeCorrelationIdRecipient : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_NotificationMessages_Type_CorrelationId_Recipient",
                table: "NotificationMessages",
                columns: new[] { "Type", "CorrelationId", "Recipient" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationMessages_Type_CorrelationId_Recipient",
                table: "NotificationMessages");
        }
    }
}
