using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleSystemPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    LegacyMongoId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoleClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: false),
                    ClaimValue = table.Column<string>(type: "text", nullable: false),
                    LegacyMongoId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleClaims_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "CreatedAt", "Description", "LegacyMongoId", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("1754c6f8-c021-41aa-b610-17088f9476f9"), new DateTimeOffset(new DateTime(2026, 2, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Administrative privileges", null, "Admin", new DateTimeOffset(new DateTime(2026, 2, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("f124fe5f-9bf2-45df-bfd2-d5d6be920016"), new DateTimeOffset(new DateTime(2026, 2, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Default role for all users", null, "User", new DateTimeOffset(new DateTime(2026, 2, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("f93f03af-ae11-4fd8-a60e-f970f89df6fb"), new DateTimeOffset(new DateTime(2026, 2, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Excluded from ranking", null, "Tester", new DateTimeOffset(new DateTime(2026, 2, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.Sql("""
                INSERT INTO "UserRoles" ("UserId", "RoleId")
                SELECT u."Id", 'f124fe5f-9bf2-45df-bfd2-d5d6be920016'::uuid
                FROM "Users" u
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "UserRoles" ur
                    WHERE ur."UserId" = u."Id" AND ur."RoleId" = 'f124fe5f-9bf2-45df-bfd2-d5d6be920016'::uuid
                );
                """);

            migrationBuilder.Sql("""
                INSERT INTO "UserRoles" ("UserId", "RoleId")
                SELECT u."Id", '1754c6f8-c021-41aa-b610-17088f9476f9'::uuid
                FROM "Users" u
                WHERE u."Admin" = TRUE
                AND NOT EXISTS (
                    SELECT 1
                    FROM "UserRoles" ur
                    WHERE ur."UserId" = u."Id" AND ur."RoleId" = '1754c6f8-c021-41aa-b610-17088f9476f9'::uuid
                );
                """);

            migrationBuilder.Sql("""
                INSERT INTO "UserRoles" ("UserId", "RoleId")
                SELECT u."Id", 'f93f03af-ae11-4fd8-a60e-f970f89df6fb'::uuid
                FROM "Users" u
                WHERE u."IsTester" = TRUE
                AND NOT EXISTS (
                    SELECT 1
                    FROM "UserRoles" ur
                    WHERE ur."UserId" = u."Id" AND ur."RoleId" = 'f93f03af-ae11-4fd8-a60e-f970f89df6fb'::uuid
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaims_RoleId_ClaimType_ClaimValue",
                table: "RoleClaims",
                columns: new[] { "RoleId", "ClaimType", "ClaimValue" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoleClaims");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "Roles");
        }
    }
}
