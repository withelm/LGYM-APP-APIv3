using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncNotificationMessageEnumStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'NotificationMessages'
                          AND column_name = 'Channel'
                          AND data_type = 'text'
                    ) THEN
                        ALTER TABLE "NotificationMessages"
                        ALTER COLUMN "Channel" TYPE integer
                        USING CASE
                            WHEN "Channel" = 'Email' THEN 0
                            WHEN "Channel" ~ '^[0-9]+$' THEN "Channel"::integer
                            ELSE 0
                        END;
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'NotificationMessages'
                          AND column_name = 'Status'
                          AND data_type = 'text'
                    ) THEN
                        ALTER TABLE "NotificationMessages"
                        ALTER COLUMN "Status" TYPE integer
                        USING CASE
                            WHEN "Status" = 'Pending' THEN 0
                            WHEN "Status" = 'Sent' THEN 1
                            WHEN "Status" = 'Failed' THEN 2
                            WHEN "Status" ~ '^[0-9]+$' THEN "Status"::integer
                            ELSE 0
                        END;
                    END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'NotificationMessages'
                          AND column_name = 'Status'
                          AND data_type = 'integer'
                    ) THEN
                        ALTER TABLE "NotificationMessages"
                        ALTER COLUMN "Status" TYPE text
                        USING CASE
                            WHEN "Status" = 0 THEN 'Pending'
                            WHEN "Status" = 1 THEN 'Sent'
                            WHEN "Status" = 2 THEN 'Failed'
                            ELSE 'Pending'
                        END;
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'NotificationMessages'
                          AND column_name = 'Channel'
                          AND data_type = 'integer'
                    ) THEN
                        ALTER TABLE "NotificationMessages"
                        ALTER COLUMN "Channel" TYPE text
                        USING CASE
                            WHEN "Channel" = 0 THEN 'Email'
                            ELSE 'Email'
                        END;
                    END IF;
                END
                $$;
                """);
        }
    }
}
