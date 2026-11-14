using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecoverableAccountDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RecoveryDeletionGeneration",
                table: "UserSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecoveryTokenHash",
                table: "UserSessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletionEffectiveAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletionGeneration",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletionRequestedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionState",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<Guid>(
                name: "LifecycleRevision",
                table: "Users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "PhotoDeletionWorkItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletionGeneration = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    IsPrefix = table.Column<bool>(type: "boolean", nullable: false),
                    ContinuationToken = table.Column<string>(type: "text", nullable: true),
                    NotBeforeUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoDeletionWorkItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_DeletionState_DeletionEffectiveAt",
                table: "Users",
                columns: new[] { "DeletionState", "DeletionEffectiveAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PhotoDeletionWorkItems_CompletedAtUtc_NotBeforeUtc",
                table: "PhotoDeletionWorkItems",
                columns: new[] { "CompletedAtUtc", "NotBeforeUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PhotoDeletionWorkItems_DeletionGeneration_StorageKey_IsPref~",
                table: "PhotoDeletionWorkItems",
                columns: new[] { "DeletionGeneration", "StorageKey", "IsPrefix" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Account deletion deadlines and committed photo cleanup must survive rollback.");
        }
    }
}
