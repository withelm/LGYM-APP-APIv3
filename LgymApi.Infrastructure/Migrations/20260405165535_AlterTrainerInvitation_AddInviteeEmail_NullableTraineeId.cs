using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlterTrainerInvitation_AddInviteeEmail_NullableTraineeId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TrainerInvitations_Users_TraineeId",
                table: "TrainerInvitations");

            migrationBuilder.AlterColumn<Guid>(
                name: "TraineeId",
                table: "TrainerInvitations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "InviteeEmail",
                table: "TrainerInvitations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_TrainerInvitations_Users_TraineeId",
                table: "TrainerInvitations",
                column: "TraineeId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TrainerInvitations_Users_TraineeId",
                table: "TrainerInvitations");

            migrationBuilder.DropColumn(
                name: "InviteeEmail",
                table: "TrainerInvitations");

            migrationBuilder.AlterColumn<Guid>(
                name: "TraineeId",
                table: "TrainerInvitations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TrainerInvitations_Users_TraineeId",
                table: "TrainerInvitations",
                column: "TraineeId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
