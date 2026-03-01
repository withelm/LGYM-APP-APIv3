using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameExecutionLogToActionExecutionLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExecutionLogs_CommandEnvelopes_CommandEnvelopeId",
                table: "ExecutionLogs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ExecutionLogs",
                table: "ExecutionLogs");

            migrationBuilder.RenameTable(
                name: "ExecutionLogs",
                newName: "ActionExecutionLogs");

            migrationBuilder.RenameIndex(
                name: "IX_ExecutionLogs_CreatedAt",
                table: "ActionExecutionLogs",
                newName: "IX_ActionExecutionLogs_CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_ExecutionLogs_CommandEnvelopeId_Status",
                table: "ActionExecutionLogs",
                newName: "IX_ActionExecutionLogs_CommandEnvelopeId_Status");

            migrationBuilder.RenameIndex(
                name: "IX_ExecutionLogs_CommandEnvelopeId_ActionType",
                table: "ActionExecutionLogs",
                newName: "IX_ActionExecutionLogs_CommandEnvelopeId_ActionType");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ActionExecutionLogs",
                table: "ActionExecutionLogs",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ActionExecutionLogs_CommandEnvelopes_CommandEnvelopeId",
                table: "ActionExecutionLogs",
                column: "CommandEnvelopeId",
                principalTable: "CommandEnvelopes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActionExecutionLogs_CommandEnvelopes_CommandEnvelopeId",
                table: "ActionExecutionLogs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ActionExecutionLogs",
                table: "ActionExecutionLogs");

            migrationBuilder.RenameTable(
                name: "ActionExecutionLogs",
                newName: "ExecutionLogs");

            migrationBuilder.RenameIndex(
                name: "IX_ActionExecutionLogs_CreatedAt",
                table: "ExecutionLogs",
                newName: "IX_ExecutionLogs_CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_ActionExecutionLogs_CommandEnvelopeId_Status",
                table: "ExecutionLogs",
                newName: "IX_ExecutionLogs_CommandEnvelopeId_Status");

            migrationBuilder.RenameIndex(
                name: "IX_ActionExecutionLogs_CommandEnvelopeId_ActionType",
                table: "ExecutionLogs",
                newName: "IX_ExecutionLogs_CommandEnvelopeId_ActionType");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ExecutionLogs",
                table: "ExecutionLogs",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ExecutionLogs_CommandEnvelopes_CommandEnvelopeId",
                table: "ExecutionLogs",
                column: "CommandEnvelopeId",
                principalTable: "CommandEnvelopes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
