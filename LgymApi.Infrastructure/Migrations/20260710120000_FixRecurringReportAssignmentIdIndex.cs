using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    public partial class FixRecurringReportAssignmentIdIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReportRequests_RecurringReportAssignmentId",
                table: "ReportRequests");

            migrationBuilder.CreateIndex(
                name: "IX_ReportRequests_RecurringReportAssignmentId",
                table: "ReportRequests",
                column: "RecurringReportAssignmentId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReportRequests_RecurringReportAssignmentId",
                table: "ReportRequests");

            migrationBuilder.CreateIndex(
                name: "IX_ReportRequests_RecurringReportAssignmentId",
                table: "ReportRequests",
                column: "RecurringReportAssignmentId",
                unique: true);
        }
    }
}
