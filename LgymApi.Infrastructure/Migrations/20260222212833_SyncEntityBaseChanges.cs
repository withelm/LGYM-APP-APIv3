using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncEntityBaseChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "TrainerTraineeLinks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "TrainerInvitations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "SupplementPlanItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "SupplementIntakeLogs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "RoleClaims",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ReportTemplateFields",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ReportSubmissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ReportRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "EmailNotificationLogs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("27965bf4-ff55-4261-8f98-218ccf00e537"),
                column: "IsDeleted",
                value: false);

            migrationBuilder.UpdateData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("97f7ea56-0032-4f18-8703-ab2d1485ad45"),
                column: "IsDeleted",
                value: false);

            migrationBuilder.UpdateData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("9dbfd057-cf88-4597-b668-2fdf16a2def6"),
                column: "IsDeleted",
                value: false);

            migrationBuilder.UpdateData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("d12f9f84-48f4-4f4b-9614-843f31ea0f96"),
                column: "IsDeleted",
                value: false);

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("1754c6f8-c021-41aa-b610-17088f9476f9"),
                column: "IsDeleted",
                value: false);

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("8c1a3db8-72a3-47cc-b3de-f5347c6ae501"),
                column: "IsDeleted",
                value: false);

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("f124fe5f-9bf2-45df-bfd2-d5d6be920016"),
                column: "IsDeleted",
                value: false);

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("f93f03af-ae11-4fd8-a60e-f970f89df6fb"),
                column: "IsDeleted",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "TrainerTraineeLinks");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "TrainerInvitations");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "SupplementPlanItems");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "SupplementIntakeLogs");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "RoleClaims");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ReportTemplateFields");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ReportSubmissions");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ReportRequests");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "EmailNotificationLogs");
        }
    }
}
