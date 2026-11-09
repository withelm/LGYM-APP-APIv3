using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseMediaUploadSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExerciseMediaUploads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetExerciseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    SourceSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PublishedSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Extension = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    Frames = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    OperationHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PreviousImage = table.Column<string>(type: "text", nullable: true),
                    ResultImage = table.Column<string>(type: "text", nullable: true),
                    PrivateObjectsCleaned = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExerciseMediaUploads", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseMediaUploads_ActorId_OperationId",
                table: "ExerciseMediaUploads",
                columns: new[] { "ActorId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseMediaUploads_PrivateObjectsCleaned_ExpiresAt",
                table: "ExerciseMediaUploads",
                columns: new[] { "PrivateObjectsCleaned", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExerciseMediaUploads");
        }
    }
}
