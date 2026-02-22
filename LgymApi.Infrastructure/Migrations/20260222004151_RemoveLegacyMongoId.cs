using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyMongoId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LegacyMongoId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LegacyMongoId",
                table: "Trainings");

            migrationBuilder.DropColumn(
                name: "LegacyMongoId",
                table: "TrainingExerciseScores");

            migrationBuilder.DropColumn(
                name: "LegacyMongoId",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "LegacyMongoId",
                table: "PlanDays");

            migrationBuilder.DropColumn(
                name: "LegacyMongoId",
                table: "PlanDayExercises");

            migrationBuilder.DropColumn(
                name: "LegacyMongoId",
                table: "Measurements");

            migrationBuilder.DropColumn(
                name: "LegacyMongoId",
                table: "MainRecords");

            migrationBuilder.DropColumn(
                name: "LegacyMongoId",
                table: "Gyms");

            migrationBuilder.DropColumn(
                name: "LegacyMongoId",
                table: "ExerciseTranslations");

            migrationBuilder.DropColumn(
                name: "LegacyMongoId",
                table: "ExerciseScores");

            migrationBuilder.DropColumn(
                name: "LegacyMongoId",
                table: "Exercises");

            migrationBuilder.DropColumn(
                name: "LegacyMongoId",
                table: "EloRegistries");

            migrationBuilder.DropColumn(
                name: "LegacyMongoId",
                table: "AppConfigs");

            migrationBuilder.DropColumn(
                name: "LegacyMongoId",
                table: "Addresses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LegacyMongoId",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyMongoId",
                table: "Trainings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyMongoId",
                table: "TrainingExerciseScores",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyMongoId",
                table: "Plans",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyMongoId",
                table: "PlanDays",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyMongoId",
                table: "PlanDayExercises",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyMongoId",
                table: "Measurements",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyMongoId",
                table: "MainRecords",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyMongoId",
                table: "Gyms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyMongoId",
                table: "ExerciseTranslations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyMongoId",
                table: "ExerciseScores",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyMongoId",
                table: "Exercises",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyMongoId",
                table: "EloRegistries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyMongoId",
                table: "AppConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyMongoId",
                table: "Addresses",
                type: "text",
                nullable: true);
        }
    }
}
