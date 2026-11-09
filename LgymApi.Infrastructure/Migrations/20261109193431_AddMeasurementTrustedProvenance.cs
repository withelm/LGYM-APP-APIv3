using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMeasurementTrustedProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Measurements_UserId", table: "Measurements");
            migrationBuilder.AddColumn<DateTimeOffset>(name: "AcceptedAt", table: "ReportSubmissions", type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "SourceCoachingEpisodeId", table: "ReportSubmissions", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "SourceCoachingEpisodeId", table: "ReportSubmissionProgressReceipts", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<DateTimeOffset>(name: "SourceReportAcceptedAt", table: "ReportSubmissionProgressReceipts", type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "SourceReportRequestId", table: "ReportSubmissionProgressReceipts", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "SourceCoachingEpisodeId", table: "ReportRequests", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<DateTimeOffset>(name: "RecordedAt", table: "Measurements", type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "SourceCoachingEpisodeId", table: "Measurements", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<DateTimeOffset>(name: "SourceReportAcceptedAt", table: "Measurements", type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "SourceReportRequestId", table: "Measurements", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "SourceReportSubmissionId", table: "Measurements", type: "uuid", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_ReportSubmissions_SourceCoachingEpisodeId", table: "ReportSubmissions", column: "SourceCoachingEpisodeId");
            migrationBuilder.CreateIndex(name: "IX_ReportSubmissionProgressReceipts_SourceCoachingEpisodeId", table: "ReportSubmissionProgressReceipts", column: "SourceCoachingEpisodeId");
            migrationBuilder.CreateIndex(name: "IX_ReportRequests_SourceCoachingEpisodeId", table: "ReportRequests", column: "SourceCoachingEpisodeId");
            migrationBuilder.CreateIndex(name: "IX_Measurements_UserId_SourceCoachingEpisodeId_SourceReportAcc~", table: "Measurements", columns: new[] { "UserId", "SourceCoachingEpisodeId", "SourceReportAcceptedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            => throw new NotSupportedException("Rolling back AddMeasurementTrustedProvenance is unsupported because it would discard trusted provenance or re-enable ambiguous legacy receipt processing.");
    }
}
