using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyMongoIdFromReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LegacyMongoId",
                table: "ReportRequests");

            migrationBuilder.DropColumn(
                name: "LegacyMongoId",
                table: "ReportSubmissions");

            migrationBuilder.DropColumn(
                name: "LegacyMongoId",
                table: "ReportTemplateFields");

            migrationBuilder.DropColumn(
                name: "LegacyMongoId",
                table: "ReportTemplates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LegacyMongoId",
                table: "ReportRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyMongoId",
                table: "ReportSubmissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyMongoId",
                table: "ReportTemplateFields",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyMongoId",
                table: "ReportTemplates",
                type: "text",
                nullable: true);
        }
    }
}
