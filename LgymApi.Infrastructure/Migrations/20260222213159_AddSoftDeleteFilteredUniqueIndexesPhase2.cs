using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteFilteredUniqueIndexesPhase2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrainerTraineeLinks_TraineeId",
                table: "TrainerTraineeLinks");

            migrationBuilder.DropIndex(
                name: "IX_TrainerInvitations_Code",
                table: "TrainerInvitations");

            migrationBuilder.DropIndex(
                name: "IX_SupplementIntakeLogs_TraineeId_PlanItemId_IntakeDate",
                table: "SupplementIntakeLogs");

            migrationBuilder.DropIndex(
                name: "IX_Roles_Name",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_RoleClaims_RoleId_ClaimType_ClaimValue",
                table: "RoleClaims");

            migrationBuilder.DropIndex(
                name: "IX_ReportTemplateFields_TemplateId_Key",
                table: "ReportTemplateFields");

            migrationBuilder.DropIndex(
                name: "IX_ReportSubmissions_ReportRequestId",
                table: "ReportSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_EmailNotificationLogs_Type_CorrelationId_RecipientEmail",
                table: "EmailNotificationLogs");

            migrationBuilder.CreateIndex(
                name: "IX_TrainerTraineeLinks_TraineeId",
                table: "TrainerTraineeLinks",
                column: "TraineeId",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_TrainerInvitations_Code",
                table: "TrainerInvitations",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_SupplementIntakeLogs_TraineeId_PlanItemId_IntakeDate",
                table: "SupplementIntakeLogs",
                columns: new[] { "TraineeId", "PlanItemId", "IntakeDate" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaims_RoleId_ClaimType_ClaimValue",
                table: "RoleClaims",
                columns: new[] { "RoleId", "ClaimType", "ClaimValue" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_ReportTemplateFields_TemplateId_Key",
                table: "ReportTemplateFields",
                columns: new[] { "TemplateId", "Key" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSubmissions_ReportRequestId",
                table: "ReportSubmissions",
                column: "ReportRequestId",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_EmailNotificationLogs_Type_CorrelationId_RecipientEmail",
                table: "EmailNotificationLogs",
                columns: new[] { "Type", "CorrelationId", "RecipientEmail" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrainerTraineeLinks_TraineeId",
                table: "TrainerTraineeLinks");

            migrationBuilder.DropIndex(
                name: "IX_TrainerInvitations_Code",
                table: "TrainerInvitations");

            migrationBuilder.DropIndex(
                name: "IX_SupplementIntakeLogs_TraineeId_PlanItemId_IntakeDate",
                table: "SupplementIntakeLogs");

            migrationBuilder.DropIndex(
                name: "IX_Roles_Name",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_RoleClaims_RoleId_ClaimType_ClaimValue",
                table: "RoleClaims");

            migrationBuilder.DropIndex(
                name: "IX_ReportTemplateFields_TemplateId_Key",
                table: "ReportTemplateFields");

            migrationBuilder.DropIndex(
                name: "IX_ReportSubmissions_ReportRequestId",
                table: "ReportSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_EmailNotificationLogs_Type_CorrelationId_RecipientEmail",
                table: "EmailNotificationLogs");

            migrationBuilder.CreateIndex(
                name: "IX_TrainerTraineeLinks_TraineeId",
                table: "TrainerTraineeLinks",
                column: "TraineeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainerInvitations_Code",
                table: "TrainerInvitations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplementIntakeLogs_TraineeId_PlanItemId_IntakeDate",
                table: "SupplementIntakeLogs",
                columns: new[] { "TraineeId", "PlanItemId", "IntakeDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaims_RoleId_ClaimType_ClaimValue",
                table: "RoleClaims",
                columns: new[] { "RoleId", "ClaimType", "ClaimValue" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportTemplateFields_TemplateId_Key",
                table: "ReportTemplateFields",
                columns: new[] { "TemplateId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportSubmissions_ReportRequestId",
                table: "ReportSubmissions",
                column: "ReportRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailNotificationLogs_Type_CorrelationId_RecipientEmail",
                table: "EmailNotificationLogs",
                columns: new[] { "Type", "CorrelationId", "RecipientEmail" },
                unique: true);
        }
    }
}
