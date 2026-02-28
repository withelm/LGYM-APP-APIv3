using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillOutboxFromNotificationMessages : Migration
    {
        private const string EmailNotificationScheduledEventType = "email.notification.scheduled";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"""
                INSERT INTO "OutboxMessages" (
                    "Id",
                    "EventType",
                    "PayloadJson",
                    "CorrelationId",
                    "Status",
                    "Attempts",
                    "NextAttemptAt",
                    "ProcessedAt",
                    "LastError",
                    "CreatedAt",
                    "UpdatedAt",
                    "IsDeleted"
                )
                SELECT
                    nm."Id",
                    '{EmailNotificationScheduledEventType}',
                    jsonb_build_object(
                        'notificationId', nm."Id",
                        'correlationId', nm."CorrelationId",
                        'recipient', nm."Recipient",
                        'notificationType', nm."Type"
                    )::text,
                    nm."CorrelationId",
                    'Pending',
                    0,
                    nm."NextAttemptAt",
                    NULL,
                    NULL,
                    nm."CreatedAt",
                    nm."UpdatedAt",
                    FALSE
                FROM "NotificationMessages" nm
                WHERE nm."IsDeleted" = FALSE
                  AND nm."Channel" = 0
                  AND nm."Status" IN (0, 2)
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "OutboxMessages" om
                      WHERE om."EventType" = '{EmailNotificationScheduledEventType}'
                        AND om."PayloadJson"::jsonb ->> 'notificationId' = nm."Id"::text
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentional no-op: irreversible data backfill migration.
        }
    }
}
