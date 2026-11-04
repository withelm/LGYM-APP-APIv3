using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainerApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrainerApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainerApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainerApplications_Users_ApplicantId",
                        column: x => x.ApplicantId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrainerApplications_Users_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrainerApplications_ApplicantId",
                table: "TrainerApplications",
                column: "ApplicantId",
                unique: true,
                filter: " \"Status\" = 'PendingApproval' AND \"IsDeleted\" = FALSE ");

            migrationBuilder.CreateIndex(
                name: "IX_TrainerApplications_ApplicantId_Attempt",
                table: "TrainerApplications",
                columns: new[] { "ApplicantId", "Attempt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainerApplications_ReviewedBy",
                table: "TrainerApplications",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_TrainerApplications_Status_CreatedAt",
                table: "TrainerApplications",
                columns: new[] { "Status", "CreatedAt" });

            // Preserve access only for existing accounts with the canonical Trainer role.
            migrationBuilder.Sql("""
                INSERT INTO "TrainerApplications"
                    ("Id", "ApplicantId", "Attempt", "Status", "DecidedAt", "CreatedAt", "UpdatedAt", "IsDeleted")
                SELECT u."Id", u."Id", 1, 'Approved', TIMESTAMPTZ '2026-11-04 00:00:00+00',
                    u."CreatedAt", TIMESTAMPTZ '2026-11-04 00:00:00+00', FALSE
                FROM "Users" u
                WHERE u."IsDeleted" = FALSE AND EXISTS (
                    SELECT 1 FROM "UserRoles" ur JOIN "Roles" r ON r."Id" = ur."RoleId"
                    WHERE ur."UserId" = u."Id" AND r."Name" = 'Trainer' AND r."IsDeleted" = FALSE
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Trainer approval audit data must be retained. Use a forward-compatible application rollback.");
        }
    }
}
