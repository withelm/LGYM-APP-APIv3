using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReportingPhotoTypeSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Photos_ReportRequestId_ViewType",
                table: "Photos");

            migrationBuilder.AddColumn<string>(
                name: "ReportPhotoSlotId",
                table: "PhotoUploadSessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourcePhotoTypeId",
                table: "PhotoUploadSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportPhotoSlotId",
                table: "Photos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourcePhotoTypeId",
                table: "Photos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReportRequestDefinitionSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionJson = table.Column<string>(type: "text", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportRequestDefinitionSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportRequestDefinitionSnapshots_ReportRequests_ReportReque~",
                        column: x => x.ReportRequestId,
                        principalTable: "ReportRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrainerPhotoTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NameMode = table.Column<string>(type: "text", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainerPhotoTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainerPhotoTypes_Users_TrainerId",
                        column: x => x.TrainerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PhotoUploadSessions_SourcePhotoTypeId",
                table: "PhotoUploadSessions",
                column: "SourcePhotoTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_ReportRequestId_ReportPhotoSlotId",
                table: "Photos",
                columns: new[] { "ReportRequestId", "ReportPhotoSlotId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE AND \"ReportPhotoSlotId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_ReportRequestId_ViewType",
                table: "Photos",
                columns: new[] { "ReportRequestId", "ViewType" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE AND \"ReportPhotoSlotId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_SourcePhotoTypeId",
                table: "Photos",
                column: "SourcePhotoTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportRequestDefinitionSnapshots_ReportRequestId",
                table: "ReportRequestDefinitionSnapshots",
                column: "ReportRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainerPhotoTypes_TrainerId_NormalizedName",
                table: "TrainerPhotoTypes",
                columns: new[] { "TrainerId", "NormalizedName" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_TrainerPhotoTypes_TrainerId_SystemKey",
                table: "TrainerPhotoTypes",
                columns: new[] { "TrainerId", "SystemKey" },
                unique: true,
                filter: "\"SystemKey\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Photos_TrainerPhotoTypes_SourcePhotoTypeId",
                table: "Photos",
                column: "SourcePhotoTypeId",
                principalTable: "TrainerPhotoTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PhotoUploadSessions_TrainerPhotoTypes_SourcePhotoTypeId",
                table: "PhotoUploadSessions",
                column: "SourcePhotoTypeId",
                principalTable: "TrainerPhotoTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Photos_TrainerPhotoTypes_SourcePhotoTypeId",
                table: "Photos");

            migrationBuilder.DropForeignKey(
                name: "FK_PhotoUploadSessions_TrainerPhotoTypes_SourcePhotoTypeId",
                table: "PhotoUploadSessions");

            migrationBuilder.DropTable(
                name: "ReportRequestDefinitionSnapshots");

            migrationBuilder.DropTable(
                name: "TrainerPhotoTypes");

            migrationBuilder.DropIndex(
                name: "IX_PhotoUploadSessions_SourcePhotoTypeId",
                table: "PhotoUploadSessions");

            migrationBuilder.DropIndex(
                name: "IX_Photos_ReportRequestId_ReportPhotoSlotId",
                table: "Photos");

            migrationBuilder.DropIndex(
                name: "IX_Photos_ReportRequestId_ViewType",
                table: "Photos");

            migrationBuilder.DropIndex(
                name: "IX_Photos_SourcePhotoTypeId",
                table: "Photos");

            migrationBuilder.DropColumn(
                name: "ReportPhotoSlotId",
                table: "PhotoUploadSessions");

            migrationBuilder.DropColumn(
                name: "SourcePhotoTypeId",
                table: "PhotoUploadSessions");

            migrationBuilder.DropColumn(
                name: "ReportPhotoSlotId",
                table: "Photos");

            migrationBuilder.DropColumn(
                name: "SourcePhotoTypeId",
                table: "Photos");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_ReportRequestId_ViewType",
                table: "Photos",
                columns: new[] { "ReportRequestId", "ViewType" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");
        }
    }
}
