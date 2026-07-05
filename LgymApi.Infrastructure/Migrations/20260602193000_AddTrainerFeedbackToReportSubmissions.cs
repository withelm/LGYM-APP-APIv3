using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    public partial class AddTrainerFeedbackToReportSubmissions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TrainerFieldCommentsJson",
                table: "ReportSubmissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrainerOverallComment",
                table: "ReportSubmissions",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TrainerFieldCommentsJson",
                table: "ReportSubmissions");

            migrationBuilder.DropColumn(
                name: "TrainerOverallComment",
                table: "ReportSubmissions");
        }
    }
}
