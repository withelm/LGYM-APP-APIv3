using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConvertEnumColumnsToText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert integer values to their string enum names before changing column type.
            migrationBuilder.Sql("""
                ALTER TABLE "NotificationMessages" ALTER COLUMN "Status" TYPE text USING CASE "Status"
                    WHEN 0 THEN 'Pending'
                    WHEN 1 THEN 'Sent'
                    WHEN 2 THEN 'Failed'
                    ELSE "Status"::text
                END;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "NotificationMessages" ALTER COLUMN "Channel" TYPE text USING CASE "Channel"
                    WHEN 0 THEN 'Email'
                    WHEN 1 THEN 'InApp'
                    ELSE "Channel"::text
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Convert string enum names back to integer values before restoring column type.
            migrationBuilder.Sql("""
                ALTER TABLE "NotificationMessages" ALTER COLUMN "Status" TYPE integer USING CASE
                    WHEN "Status" = 'Pending' THEN 0
                    WHEN "Status" = 'Sent'    THEN 1
                    WHEN "Status" = 'Failed'  THEN 2
                    WHEN "Status" ~ '^[0-9]+$' THEN "Status"::integer
                    ELSE CAST("Status" AS integer)
                END;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "NotificationMessages" ALTER COLUMN "Channel" TYPE integer USING CASE
                    WHEN "Channel" = 'Email' THEN 0
                    WHEN "Channel" = 'InApp' THEN 1
                    WHEN "Channel" ~ '^[0-9]+$' THEN "Channel"::integer
                    ELSE CAST("Channel" AS integer)
                END;
                """);
        }
    }
}
