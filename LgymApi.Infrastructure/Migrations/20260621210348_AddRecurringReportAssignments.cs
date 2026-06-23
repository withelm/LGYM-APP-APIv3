using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringReportAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TrainerOverallComment",
                table: "ReportSubmissions",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TrainerFeedbackAddedAt",
                table: "ReportSubmissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TrainerFeedbackReadAt",
                table: "ReportSubmissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecurringReportAssignmentId",
                table: "ReportRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RecurringReportAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TraineeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntervalValue = table.Column<int>(type: "integer", nullable: false),
                    IntervalUnit = table.Column<string>(type: "text", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CurrentReportRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastRequestCreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextEligibleAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringReportAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringReportAssignments_ReportRequests_CurrentReportRequ~",
                        column: x => x.CurrentReportRequestId,
                        principalTable: "ReportRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RecurringReportAssignments_ReportTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "ReportTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecurringReportAssignments_Users_TraineeId",
                        column: x => x.TraineeId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecurringReportAssignments_Users_TrainerId",
                        column: x => x.TrainerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportRequests_RecurringReportAssignmentId",
                table: "ReportRequests",
                column: "RecurringReportAssignmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecurringReportAssignments_CurrentReportRequestId",
                table: "RecurringReportAssignments",
                column: "CurrentReportRequestId",
                unique: true,
                filter: "\"CurrentReportRequestId\" IS NOT NULL AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringReportAssignments_TemplateId",
                table: "RecurringReportAssignments",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringReportAssignments_TraineeId_NextEligibleAt",
                table: "RecurringReportAssignments",
                columns: new[] { "TraineeId", "NextEligibleAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringReportAssignments_TrainerId_TraineeId_IsActive",
                table: "RecurringReportAssignments",
                columns: new[] { "TrainerId", "TraineeId", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_ReportRequests_RecurringReportAssignments_RecurringReportAs~",
                table: "ReportRequests",
                column: "RecurringReportAssignmentId",
                principalTable: "RecurringReportAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReportRequests_RecurringReportAssignments_RecurringReportAs~",
                table: "ReportRequests");

            migrationBuilder.DropTable(
                name: "RecurringReportAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ReportRequests_RecurringReportAssignmentId",
                table: "ReportRequests");

            migrationBuilder.DropColumn(
                name: "TrainerFeedbackAddedAt",
                table: "ReportSubmissions");

            migrationBuilder.DropColumn(
                name: "TrainerFeedbackReadAt",
                table: "ReportSubmissions");

            migrationBuilder.DropColumn(
                name: "RecurringReportAssignmentId",
                table: "ReportRequests");

            migrationBuilder.AlterColumn<string>(
                name: "TrainerOverallComment",
                table: "ReportSubmissions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);
        }
    }
}
