using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueShareCodeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH duplicate_codes AS (
                    SELECT "Id"
                    FROM (
                        SELECT
                            "Id",
                            ROW_NUMBER() OVER (
                                PARTITION BY "ShareCode"
                                ORDER BY "CreatedAt", "Id"
                            ) AS "RowNumber"
                        FROM "Plans"
                        WHERE "IsDeleted" = FALSE AND "ShareCode" IS NOT NULL
                    ) ranked
                    WHERE ranked."RowNumber" > 1
                )
                UPDATE "Plans"
                SET "ShareCode" = NULL
                WHERE "Id" IN (SELECT "Id" FROM duplicate_codes);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Plans_ShareCode",
                table: "Plans",
                column: "ShareCode",
                unique: true,
                filter: "\"IsDeleted\" = FALSE AND \"ShareCode\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Plans_ShareCode",
                table: "Plans");
        }
    }
}
