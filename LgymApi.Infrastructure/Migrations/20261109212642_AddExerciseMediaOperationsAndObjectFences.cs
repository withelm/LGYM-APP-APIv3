using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseMediaOperationsAndObjectFences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExerciseMediaObjectFences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PublicUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    IsRetired = table.Column<bool>(type: "boolean", nullable: false),
                    LastCleanupAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastDeleteSucceeded = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExerciseMediaObjectFences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExerciseMediaUploads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetExerciseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PublishedSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Extension = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PreviousImage = table.Column<string>(type: "text", nullable: true),
                    ResultImage = table.Column<string>(type: "text", nullable: true),
                    ObjectKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RejectionStatusCode = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExerciseMediaUploads", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseMediaObjectFences_LastCleanupAt",
                table: "ExerciseMediaObjectFences",
                column: "LastCleanupAt");

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseMediaObjectFences_ObjectKey",
                table: "ExerciseMediaObjectFences",
                column: "ObjectKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseMediaUploads_ActorId_OperationId",
                table: "ExerciseMediaUploads",
                columns: new[] { "ActorId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseMediaUploads_ObjectKey_ExpiresAt",
                table: "ExerciseMediaUploads",
                columns: new[] { "ObjectKey", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseMediaUploads_Status_ExpiresAt",
                table: "ExerciseMediaUploads",
                columns: new[] { "Status", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException("Exercise media receipts and permanent retired-key fences must be retained; migration downgrade is not supported.");
        }
    }
}
