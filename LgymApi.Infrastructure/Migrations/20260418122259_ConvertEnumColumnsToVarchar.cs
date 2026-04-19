using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConvertEnumColumnsToVarchar : Migration
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
                ALTER TABLE "NotificationMessages" ALTER COLUMN "Status" TYPE integer USING CASE "Status"
                    WHEN 'Pending' THEN 0
                    WHEN 'Sent'    THEN 1
                    WHEN 'Failed'  THEN 2
                    ELSE 0
                END;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "NotificationMessages" ALTER COLUMN "Channel" TYPE integer USING CASE "Channel"
                    WHEN 'Email' THEN 0
                    WHEN 'InApp' THEN 1
                    ELSE 0
                END;
                """);
        }
    }
}
