using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReliabilityOutboxAndIdempotencySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CommandEnvelopes_CorrelationId",
                table: "CommandEnvelopes");

            migrationBuilder.DropIndex(
                name: "IX_CommandEnvelopes_CorrelationId_Status",
                table: "CommandEnvelopes");

            migrationBuilder.AddColumn<string>(
                name: "DeadLetterReason",
                table: "NotificationMessages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DispatchedAt",
                table: "NotificationMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeadLettered",
                table: "NotificationMessages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SchedulerJobId",
                table: "NotificationMessages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DispatchedAt",
                table: "CommandEnvelopes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SchedulerJobId",
                table: "CommandEnvelopes",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApiIdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ScopeTuple = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "integer", nullable: false),
                    ResponseBodyJson = table.Column<string>(type: "text", nullable: false),
                    CommandEnvelopeId = table.Column<Guid>(type: "uuid", nullable: true),
                    NotificationMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiIdempotencyRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommandEnvelopes_CorrelationId",
                table: "CommandEnvelopes",
                column: "CorrelationId",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_ApiIdempotencyRecords_ScopeTuple_IdempotencyKey",
                table: "ApiIdempotencyRecords",
                columns: new[] { "ScopeTuple", "IdempotencyKey" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_ApiIdempotencyRecords_ScopeTuple_IdempotencyKey_RequestFing~",
                table: "ApiIdempotencyRecords",
                columns: new[] { "ScopeTuple", "IdempotencyKey", "RequestFingerprint" },
                filter: "\"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiIdempotencyRecords");

            migrationBuilder.DropIndex(
                name: "IX_CommandEnvelopes_CorrelationId",
                table: "CommandEnvelopes");

            migrationBuilder.DropColumn(
                name: "DeadLetterReason",
                table: "NotificationMessages");

            migrationBuilder.DropColumn(
                name: "DispatchedAt",
                table: "NotificationMessages");

            migrationBuilder.DropColumn(
                name: "IsDeadLettered",
                table: "NotificationMessages");

            migrationBuilder.DropColumn(
                name: "SchedulerJobId",
                table: "NotificationMessages");

            migrationBuilder.DropColumn(
                name: "DispatchedAt",
                table: "CommandEnvelopes");

            migrationBuilder.DropColumn(
                name: "SchedulerJobId",
                table: "CommandEnvelopes");

            migrationBuilder.CreateIndex(
                name: "IX_CommandEnvelopes_CorrelationId",
                table: "CommandEnvelopes",
                column: "CorrelationId",
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_CommandEnvelopes_CorrelationId_Status",
                table: "CommandEnvelopes",
                columns: new[] { "CorrelationId", "Status" },
                filter: "\"IsDeleted\" = FALSE");
        }
    }
}
