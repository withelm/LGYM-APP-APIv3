using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentsPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentCheckouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientIdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProviderSessionReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentCheckouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentCheckouts_Users_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentEventReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentCheckoutId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderEventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentEventReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentEventReceipts_PaymentCheckouts_PaymentCheckoutId",
                        column: x => x.PaymentCheckoutId,
                        principalTable: "PaymentCheckouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentCheckouts_AccountId_ClientIdempotencyKey",
                table: "PaymentCheckouts",
                columns: new[] { "AccountId", "ClientIdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentCheckouts_ProviderSessionReference",
                table: "PaymentCheckouts",
                column: "ProviderSessionReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEventReceipts_PaymentCheckoutId",
                table: "PaymentEventReceipts",
                column: "PaymentCheckoutId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEventReceipts_ProviderEventId",
                table: "PaymentEventReceipts",
                column: "ProviderEventId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentEventReceipts");

            migrationBuilder.DropTable(
                name: "PaymentCheckouts");
        }
    }
}
