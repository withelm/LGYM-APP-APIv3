using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConfirmAccountDeletionSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DeletionScheduledGeneration",
                table: "Users",
                type: "uuid",
                nullable: true);
            migrationBuilder.Sql("""
                UPDATE "Users"
                SET "DeletionScheduledGeneration" = "DeletionGeneration"
                WHERE "DeletionState" IN ('PendingDeletion', 'Finalizing')
                  AND "DeletionGeneration" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Account deletion scheduling confirmations must survive rollback.");
        }
    }
}
