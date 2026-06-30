using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTraineeNotesModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TraineeNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TraineeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Content = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    VisibleToTrainee = table.Column<bool>(type: "boolean", nullable: false),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    LastUpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TraineeNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TraineeNotes_Users_LastUpdatedByUserId",
                        column: x => x.LastUpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TraineeNotes_Users_TraineeId",
                        column: x => x.TraineeId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TraineeNotes_Users_TrainerId",
                        column: x => x.TrainerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TraineeNoteHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TraineeNoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PreviousContent = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    NewContent = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    ChangeType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TraineeNoteHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TraineeNoteHistories_TraineeNotes_TraineeNoteId",
                        column: x => x.TraineeNoteId,
                        principalTable: "TraineeNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TraineeNoteHistories_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TraineeNoteHistories_ChangedByUserId",
                table: "TraineeNoteHistories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TraineeNoteHistories_TraineeNoteId_ChangedAt",
                table: "TraineeNoteHistories",
                columns: new[] { "TraineeNoteId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TraineeNotes_LastUpdatedByUserId",
                table: "TraineeNotes",
                column: "LastUpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TraineeNotes_TraineeId_VisibleToTrainee_LastUpdatedAt",
                table: "TraineeNotes",
                columns: new[] { "TraineeId", "VisibleToTrainee", "LastUpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TraineeNotes_TrainerId_TraineeId_LastUpdatedAt",
                table: "TraineeNotes",
                columns: new[] { "TrainerId", "TraineeId", "LastUpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TraineeNoteHistories");

            migrationBuilder.DropTable(
                name: "TraineeNotes");
        }
    }
}
