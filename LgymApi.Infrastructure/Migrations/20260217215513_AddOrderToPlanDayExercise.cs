using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderToPlanDayExercise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "PlanDayExercises",
                type: "integer",
                nullable: false,
                defaultValue: 0);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Order",
                table: "PlanDayExercises");
        }
    }
}
