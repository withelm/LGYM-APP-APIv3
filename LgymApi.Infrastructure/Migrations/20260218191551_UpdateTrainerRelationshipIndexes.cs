using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTrainerRelationshipIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrainerTraineeLinks_TrainerId_TraineeId",
                table: "TrainerTraineeLinks");

            migrationBuilder.CreateIndex(
                name: "IX_TrainerTraineeLinks_TrainerId_TraineeId",
                table: "TrainerTraineeLinks",
                columns: new[] { "TrainerId", "TraineeId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrainerTraineeLinks_TrainerId_TraineeId",
                table: "TrainerTraineeLinks");

            migrationBuilder.CreateIndex(
                name: "IX_TrainerTraineeLinks_TrainerId_TraineeId",
                table: "TrainerTraineeLinks",
                columns: new[] { "TrainerId", "TraineeId" },
                unique: true);
        }
    }
}
