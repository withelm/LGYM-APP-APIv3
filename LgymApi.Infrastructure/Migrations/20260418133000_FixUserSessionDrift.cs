using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixUserSessionDrift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserSessions_Users_UserId",
                table: "UserSessions");

            migrationBuilder.AlterColumn<string>(
                name: "Jti",
                table: "UserSessions",
                type: "text",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_UserSessions_Users_UserId",
                table: "UserSessions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This migration is intentionally not reversible.
            // Previously the column "Jti" was converted from Guid (uuid) to string (text).
            // Once the application stores arbitrary JWT IDs (non-GUID values) in this column,
            // attempting to coerce back to uuid will fail for those rows. Providing an
            // automatic rollback that casts text -> uuid is unsafe and misleading.
            //
            // If you need to revert this migration, perform a manual data migration first:
            // 1. Ensure all values in "UserSessions"."Jti" are valid UUIDs.
            // 2. Then alter the column type back to uuid.
            //
            // We throw NotSupportedException to make the irreversibility explicit.
            throw new NotSupportedException("The migration 'FixUserSessionDrift' is not reversible: UserSessions.Jti was converted from Guid to string and may contain non-GUID values. To rollback, first convert or remove non-GUID Jti values and then alter the column type manually.");
        }
    }
}
