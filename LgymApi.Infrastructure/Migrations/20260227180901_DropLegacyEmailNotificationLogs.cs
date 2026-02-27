using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyEmailNotificationLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO "NotificationMessages" (
                    "Id",
                    "Channel",
                    "Type",
                    "CorrelationId",
                    "Recipient",
                    "PayloadJson",
                    "Status",
                    "Attempts",
                    "NextAttemptAt",
                    "LastError",
                    "LastAttemptAt",
                    "SentAt",
                    "CreatedAt",
                    "UpdatedAt",
                    "IsDeleted"
                )
                SELECT
                    legacy."Id",
                    0,
                    legacy."Type",
                    legacy."CorrelationId",
                    legacy."RecipientEmail",
                    legacy."PayloadJson",
                    CASE legacy."Status"
                        WHEN 'Pending' THEN 0
                        WHEN 'Sent' THEN 1
                        WHEN 'Failed' THEN 2
                        ELSE 0
                    END,
                    legacy."Attempts",
                    NULL,
                    legacy."LastError",
                    legacy."LastAttemptAt",
                    legacy."SentAt",
                    legacy."CreatedAt",
                    legacy."UpdatedAt",
                    legacy."IsDeleted"
                FROM "EmailNotificationLogs" AS legacy
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "NotificationMessages" AS current
                    WHERE current."Channel" = 0
                      AND current."Type" = legacy."Type"
                      AND current."CorrelationId" = legacy."CorrelationId"
                      AND current."Recipient" = legacy."RecipientEmail"
                      AND current."IsDeleted" = FALSE
                );
                """);

            migrationBuilder.DropTable(
                name: "EmailNotificationLogs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailNotificationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    RecipientEmail = table.Column<string>(type: "text", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailNotificationLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailNotificationLogs_Status_CreatedAt",
                table: "EmailNotificationLogs",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailNotificationLogs_Type_CorrelationId_RecipientEmail",
                table: "EmailNotificationLogs",
                columns: new[] { "Type", "CorrelationId", "RecipientEmail" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");
        }
    }
}
