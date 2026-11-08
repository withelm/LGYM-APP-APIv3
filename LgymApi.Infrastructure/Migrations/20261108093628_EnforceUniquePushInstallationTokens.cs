using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUniquePushInstallationTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "RegistrationRevision",
                table: "PushInstallations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AlterColumn<string>(
                name: "FcmToken",
                table: "PushInstallations",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512);

            migrationBuilder.Sql("""
                UPDATE "PushInstallations"
                SET "FcmToken" = NULL,
                    "UserId" = NULL,
                    "SessionId" = NULL,
                    "DisabledAt" = COALESCE("DisabledAt", CURRENT_TIMESTAMP),
                    "DisabledReason" = 'DuplicateFcmTokenMigration',
                    "IsDeleted" = TRUE,
                    "UpdatedAt" = CURRENT_TIMESTAMP
                WHERE "FcmToken" IN (
                    SELECT "FcmToken"
                    FROM "PushInstallations"
                    WHERE "FcmToken" IS NOT NULL
                    GROUP BY "FcmToken"
                    HAVING COUNT(*) > 1
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PushInstallations_FcmToken",
                table: "PushInstallations",
                column: "FcmToken",
                unique: true,
                filter: "\"FcmToken\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Downgrading FCM token uniqueness is unsafe after duplicate installations have been tombstoned and their tokens cleared.");
        }
    }
}
