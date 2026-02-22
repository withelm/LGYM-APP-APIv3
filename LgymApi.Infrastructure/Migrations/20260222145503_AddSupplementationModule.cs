using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplementationModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.CreateTable(
                name: "SupplementPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TraineeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplementPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplementPlans_Users_TraineeId",
                        column: x => x.TraineeId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplementPlans_Users_TrainerId",
                        column: x => x.TrainerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplementPlanItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplementName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Dosage = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DaysOfWeekMask = table.Column<int>(type: "integer", nullable: false),
                    TimeOfDay = table.Column<TimeSpan>(type: "interval", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplementPlanItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplementPlanItems_SupplementPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "SupplementPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplementIntakeLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TraineeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntakeDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TakenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplementIntakeLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplementIntakeLogs_SupplementPlanItems_PlanItemId",
                        column: x => x.PlanItemId,
                        principalTable: "SupplementPlanItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplementIntakeLogs_Users_TraineeId",
                        column: x => x.TraineeId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplementIntakeLogs_PlanItemId",
                table: "SupplementIntakeLogs",
                column: "PlanItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplementIntakeLogs_TraineeId_PlanItemId_IntakeDate",
                table: "SupplementIntakeLogs",
                columns: new[] { "TraineeId", "PlanItemId", "IntakeDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplementPlanItems_PlanId_Order_TimeOfDay",
                table: "SupplementPlanItems",
                columns: new[] { "PlanId", "Order", "TimeOfDay" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplementPlans_TraineeId_IsActive",
                table: "SupplementPlans",
                columns: new[] { "TraineeId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplementPlans_TrainerId_TraineeId_CreatedAt",
                table: "SupplementPlans",
                columns: new[] { "TrainerId", "TraineeId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplementIntakeLogs");

            migrationBuilder.DropTable(
                name: "SupplementPlanItems");

            migrationBuilder.DropTable(
                name: "SupplementPlans");
        }
    }
}
