using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringReportScheduleChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ScheduleRevision",
                table: "RecurringReportAssignments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceCoachingEpisodeId",
                table: "RecurringReportAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "RecurringReportAssignments" AS assignment
                SET "SourceCoachingEpisodeId" = link."EpisodeId"
                FROM "TrainerTraineeLinks" AS link
                INNER JOIN "CoachingEpisodes" AS episode ON episode."Id" = link."EpisodeId"
                WHERE assignment."SourceCoachingEpisodeId" IS NULL
                  AND assignment."TrainerId" = link."TrainerId"
                  AND assignment."TraineeId" = link."TraineeId"
                  AND link."IsDeleted" = FALSE
                  AND episode."IsDeleted" = FALSE
                  AND episode."EndedAt" IS NULL
                  AND (
                      episode."StartedAt" <= assignment."CreatedAt"
                      OR (episode."StartedAt" IS NULL
                          AND episode."StartedAtProvenance" = 'ExistingActiveLinkObservedDuringMigration'));
                """);

            migrationBuilder.CreateTable(
                name: "RecurringReportScheduleChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecurringReportAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ScheduleRevision = table.Column<int>(type: "integer", nullable: false),
                    OldScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProposedScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SourceProposalId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringReportScheduleChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringReportScheduleChanges_RecurringReportAssignments_R~",
                        column: x => x.RecurringReportAssignmentId,
                        principalTable: "RecurringReportAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecurringReportScheduleChanges_RecurringReportScheduleChang~",
                        column: x => x.SourceProposalId,
                        principalTable: "RecurringReportScheduleChanges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecurringReportScheduleChanges_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecurringReportScheduleChanges_Users_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringReportAssignments_SourceCoachingEpisodeId",
                table: "RecurringReportAssignments",
                column: "SourceCoachingEpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringReportScheduleChanges_RecurringReportAssignmentId_~",
                table: "RecurringReportScheduleChanges",
                columns: new[] { "RecurringReportAssignmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringReportScheduleChanges_RequestedByUserId",
                table: "RecurringReportScheduleChanges",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringReportScheduleChanges_ResolvedByUserId",
                table: "RecurringReportScheduleChanges",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringReportScheduleChanges_SourceProposalId",
                table: "RecurringReportScheduleChanges",
                column: "SourceProposalId");

            migrationBuilder.CreateIndex(
                name: "UX_RecurringReportScheduleChanges_ActiveOverride",
                table: "RecurringReportScheduleChanges",
                columns: new[] { "RecurringReportAssignmentId", "Kind", "Status" },
                unique: true,
                filter: "\"Kind\" = 'TrainerOverride' AND \"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "UX_RecurringReportScheduleChanges_PendingProposal",
                table: "RecurringReportScheduleChanges",
                columns: new[] { "RecurringReportAssignmentId", "Kind", "Status" },
                unique: true,
                filter: "\"Kind\" = 'TraineeProposal' AND \"Status\" = 'Pending'");

            migrationBuilder.AddForeignKey(
                name: "FK_RecurringReportAssignments_CoachingEpisodes_SourceCoachingE~",
                table: "RecurringReportAssignments",
                column: "SourceCoachingEpisodeId",
                principalTable: "CoachingEpisodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecurringReportAssignments_CoachingEpisodes_SourceCoachingE~",
                table: "RecurringReportAssignments");

            migrationBuilder.DropTable(
                name: "RecurringReportScheduleChanges");

            migrationBuilder.DropIndex(
                name: "IX_RecurringReportAssignments_SourceCoachingEpisodeId",
                table: "RecurringReportAssignments");

            migrationBuilder.DropColumn(
                name: "ScheduleRevision",
                table: "RecurringReportAssignments");

            migrationBuilder.DropColumn(
                name: "SourceCoachingEpisodeId",
                table: "RecurringReportAssignments");
        }
    }
}
