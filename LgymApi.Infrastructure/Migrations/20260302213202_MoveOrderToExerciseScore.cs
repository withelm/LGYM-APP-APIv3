using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveOrderToExerciseScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add Order column to ExerciseScores table
            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "ExerciseScores",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Step 2: Transfer Order values from TrainingExerciseScores to ExerciseScores
            migrationBuilder.Sql(@"
                UPDATE ""ExerciseScores"" es
                SET ""Order"" = tes.""Order""
                FROM ""TrainingExerciseScores"" tes
                WHERE es.""Id"" = tes.""ExerciseScoreId"";
            ");

            // Step 3: Drop Order column from TrainingExerciseScores table
            migrationBuilder.DropColumn(
                name: "Order",
                table: "TrainingExerciseScores");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add Order column back to TrainingExerciseScores table
            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "TrainingExerciseScores",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Step 2: Restore Order values from ExerciseScores to TrainingExerciseScores
            migrationBuilder.Sql(@"
                UPDATE ""TrainingExerciseScores"" tes
                SET ""Order"" = es.""Order""
                FROM ""ExerciseScores"" es
                WHERE tes.""ExerciseScoreId"" = es.""Id"";
            ");

            // Step 3: Drop Order column from ExerciseScores table
            migrationBuilder.DropColumn(
                name: "Order",
                table: "ExerciseScores");
        }
    }
}
