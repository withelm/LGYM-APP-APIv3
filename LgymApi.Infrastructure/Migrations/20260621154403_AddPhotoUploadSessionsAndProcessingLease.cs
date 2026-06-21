using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoUploadSessionsAndProcessingLease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProcessingStartedAtUtc",
                table: "CommandEnvelopes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PhotoUploadSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "text", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InitiatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ViewType = table.Column<string>(type: "text", nullable: false),
                    DeclaredContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DeclaredSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedPhotoId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoUploadSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhotoUploadSessions_Photos_CompletedPhotoId",
                        column: x => x.CompletedPhotoId,
                        principalTable: "Photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PhotoUploadSessions_ReportRequests_ReportRequestId",
                        column: x => x.ReportRequestId,
                        principalTable: "ReportRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PhotoUploadSessions_Users_InitiatedByUserId",
                        column: x => x.InitiatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhotoUploadSessions_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommandEnvelopes_Status_ProcessingStartedAtUtc",
                table: "CommandEnvelopes",
                columns: new[] { "Status", "ProcessingStartedAtUtc" },
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoUploadSessions_CompletedPhotoId",
                table: "PhotoUploadSessions",
                column: "CompletedPhotoId");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoUploadSessions_InitiatedByUserId",
                table: "PhotoUploadSessions",
                column: "InitiatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoUploadSessions_OwnerUserId_CreatedAt",
                table: "PhotoUploadSessions",
                columns: new[] { "OwnerUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PhotoUploadSessions_ReportRequestId_ViewType",
                table: "PhotoUploadSessions",
                columns: new[] { "ReportRequestId", "ViewType" });

            migrationBuilder.CreateIndex(
                name: "IX_PhotoUploadSessions_Status_ExpiresAtUtc",
                table: "PhotoUploadSessions",
                columns: new[] { "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PhotoUploadSessions_StorageKey",
                table: "PhotoUploadSessions",
                column: "StorageKey",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhotoUploadSessions");

            migrationBuilder.DropIndex(
                name: "IX_CommandEnvelopes_Status_ProcessingStartedAtUtc",
                table: "CommandEnvelopes");

            migrationBuilder.DropColumn(
                name: "ProcessingStartedAtUtc",
                table: "CommandEnvelopes");
        }
    }
}
