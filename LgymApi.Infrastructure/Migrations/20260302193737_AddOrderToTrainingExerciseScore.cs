using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderToTrainingExerciseScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "TrainingExerciseScores",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"WITH ordered AS (SELECT ""Id"", ROW_NUMBER() OVER (PARTITION BY ""TrainingId"" ORDER BY ""CreatedAt"") - 1 AS new_order FROM ""TrainingExerciseScores"") UPDATE ""TrainingExerciseScores"" SET ""Order"" = ordered.new_order FROM ordered WHERE ""TrainingExerciseScores"".""Id"" = ordered.""Id"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Order",
                table: "TrainingExerciseScores");
        }
    }
}
