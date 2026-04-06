using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainerAccessClaim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBlocked",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.InsertData(
                table: "RoleClaims",
                columns: new[] { "Id", "ClaimType", "ClaimValue", "CreatedAt", "IsDeleted", "RoleId", "UpdatedAt" },
                values: new object[] { new Guid("a3b7c9d1-4e5f-6a7b-8c9d-0e1f2a3b4c5d"), "permission", "trainer.access", new DateTimeOffset(new DateTime(2026, 2, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, new Guid("8c1a3db8-72a3-47cc-b3de-f5347c6ae501"), new DateTimeOffset(new DateTime(2026, 2, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("a3b7c9d1-4e5f-6a7b-8c9d-0e1f2a3b4c5d"));

            migrationBuilder.DropColumn(
                name: "IsBlocked",
                table: "Users");
        }
    }
}
