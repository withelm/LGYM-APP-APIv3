using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedRolePermissionClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "RoleClaims",
                columns: new[] { "Id", "ClaimType", "ClaimValue", "CreatedAt", "LegacyMongoId", "RoleId", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("27965bf4-ff55-4261-8f98-218ccf00e537"), "permission", "exercises.global.manage", new DateTimeOffset(new DateTime(2026, 2, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("1754c6f8-c021-41aa-b610-17088f9476f9"), new DateTimeOffset(new DateTime(2026, 2, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("97f7ea56-0032-4f18-8703-ab2d1485ad45"), "permission", "users.roles.manage", new DateTimeOffset(new DateTime(2026, 2, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("1754c6f8-c021-41aa-b610-17088f9476f9"), new DateTimeOffset(new DateTime(2026, 2, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("9dbfd057-cf88-4597-b668-2fdf16a2def6"), "permission", "admin.access", new DateTimeOffset(new DateTime(2026, 2, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("1754c6f8-c021-41aa-b610-17088f9476f9"), new DateTimeOffset(new DateTime(2026, 2, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("d12f9f84-48f4-4f4b-9614-843f31ea0f96"), "permission", "appconfig.manage", new DateTimeOffset(new DateTime(2026, 2, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("1754c6f8-c021-41aa-b610-17088f9476f9"), new DateTimeOffset(new DateTime(2026, 2, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("27965bf4-ff55-4261-8f98-218ccf00e537"));

            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("97f7ea56-0032-4f18-8703-ab2d1485ad45"));

            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("9dbfd057-cf88-4597-b668-2fdf16a2def6"));

            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("d12f9f84-48f4-4f4b-9614-843f31ea0f96"));
        }
    }
}
