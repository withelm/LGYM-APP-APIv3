using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainerApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TrainerApplicationCreatedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TrainerApprovalDecidedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TrainerApprovalReviewedBy",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrainerApprovalStatus",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_TrainerApprovalReviewedBy",
                table: "Users",
                column: "TrainerApprovalReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TrainerApprovalStatus_TrainerApplicationCreatedAt",
                table: "Users",
                columns: new[] { "TrainerApprovalStatus", "TrainerApplicationCreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_TrainerApprovalReviewedBy",
                table: "Users",
                column: "TrainerApprovalReviewedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Only pre-existing, non-deleted Trainer role holders are grandfathered.
            // No role, relationship, plan, or ordinary account is changed. A fixed
            // timestamp and null reviewer distinguish this one-time legacy decision.
            migrationBuilder.Sql("""
                UPDATE "Users" AS u
                SET "TrainerApprovalStatus" = 'Approved',
                    "TrainerApplicationCreatedAt" = u."CreatedAt",
                    "TrainerApprovalDecidedAt" = TIMESTAMPTZ '2026-11-04 00:00:00+00'
                WHERE u."IsDeleted" = FALSE AND u."TrainerApprovalStatus" IS NULL
                  AND EXISTS (
                    SELECT 1 FROM "UserRoles" ur JOIN "Roles" r ON r."Id" = ur."RoleId"
                    WHERE ur."UserId" = u."Id" AND r."Name" = 'Trainer' AND r."IsDeleted" = FALSE
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Trainer approval audit data must be retained. Use a forward-compatible application rollback.");
        }
    }
}
