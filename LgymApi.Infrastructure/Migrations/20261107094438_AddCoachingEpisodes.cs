using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCoachingEpisodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EpisodeId",
                table: "TrainerTraineeLinks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CoachingEpisodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TraineeId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartedAtProvenance = table.Column<string>(type: "text", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AuthorizationRevision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoachingEpisodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoachingEpisodes_Users_TraineeId",
                        column: x => x.TraineeId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CoachingEpisodes_Users_TrainerId",
                        column: x => x.TrainerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                INSERT INTO "CoachingEpisodes" ("Id", "TrainerId", "TraineeId", "StartedAt", "StartedAtProvenance", "EndedAt", "AuthorizationRevision", "CreatedAt", "UpdatedAt", "IsDeleted")
                SELECT (
                    substr(md5("Id"::text || ':LGYM-213'), 1, 8) || '-' ||
                    substr(md5("Id"::text || ':LGYM-213'), 9, 4) || '-' ||
                    substr(md5("Id"::text || ':LGYM-213'), 13, 4) || '-' ||
                    substr(md5("Id"::text || ':LGYM-213'), 17, 4) || '-' ||
                    substr(md5("Id"::text || ':LGYM-213'), 21, 12)
                )::uuid, "TrainerId", "TraineeId", NULL, 'ExistingActiveLinkObservedDuringMigration', NULL, 0, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, FALSE
                FROM "TrainerTraineeLinks"
                WHERE "IsDeleted" = FALSE;

                UPDATE "TrainerTraineeLinks"
                SET "EpisodeId" = (
                    substr(md5("Id"::text || ':LGYM-213'), 1, 8) || '-' ||
                    substr(md5("Id"::text || ':LGYM-213'), 9, 4) || '-' ||
                    substr(md5("Id"::text || ':LGYM-213'), 13, 4) || '-' ||
                    substr(md5("Id"::text || ':LGYM-213'), 17, 4) || '-' ||
                    substr(md5("Id"::text || ':LGYM-213'), 21, 12)
                )::uuid
                WHERE "IsDeleted" = FALSE;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_TrainerTraineeLinks_EpisodeId",
                table: "TrainerTraineeLinks",
                column: "EpisodeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoachingEpisodes_TraineeId",
                table: "CoachingEpisodes",
                column: "TraineeId");

            migrationBuilder.CreateIndex(
                name: "IX_CoachingEpisodes_TrainerId_TraineeId",
                table: "CoachingEpisodes",
                columns: new[] { "TrainerId", "TraineeId" });

            migrationBuilder.AddForeignKey(
                name: "FK_TrainerTraineeLinks_CoachingEpisodes_EpisodeId",
                table: "TrainerTraineeLinks",
                column: "EpisodeId",
                principalTable: "CoachingEpisodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                CREATE FUNCTION "FenceActiveCoachingEpisode"() RETURNS trigger AS $function$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        UPDATE "CoachingEpisodes"
                        SET "EndedAt" = CURRENT_TIMESTAMP,
                            "AuthorizationRevision" = "AuthorizationRevision" + 1,
                            "UpdatedAt" = CURRENT_TIMESTAMP
                        WHERE "Id" = OLD."EpisodeId" AND "EndedAt" IS NULL;
                        RETURN OLD;
                    END IF;

                    IF TG_OP = 'UPDATE' AND (
                        NEW."TrainerId" IS DISTINCT FROM OLD."TrainerId" OR
                        NEW."TraineeId" IS DISTINCT FROM OLD."TraineeId" OR
                        NEW."EpisodeId" IS DISTINCT FROM OLD."EpisodeId") THEN
                        RAISE EXCEPTION 'Active coaching relationship identity is immutable';
                    END IF;

                    IF TG_OP = 'UPDATE' AND OLD."IsDeleted" = FALSE AND NEW."IsDeleted" = TRUE THEN
                        UPDATE "CoachingEpisodes"
                        SET "EndedAt" = CURRENT_TIMESTAMP,
                            "AuthorizationRevision" = "AuthorizationRevision" + 1,
                            "UpdatedAt" = CURRENT_TIMESTAMP
                        WHERE "Id" = OLD."EpisodeId" AND "EndedAt" IS NULL;
                        RETURN NEW;
                    END IF;

                    IF NEW."IsDeleted" = FALSE AND (
                        NEW."EpisodeId" IS NULL OR
                        NOT EXISTS (
                            SELECT 1
                            FROM "CoachingEpisodes" episode
                            WHERE episode."Id" = NEW."EpisodeId"
                              AND episode."TrainerId" = NEW."TrainerId"
                              AND episode."TraineeId" = NEW."TraineeId"
                              AND episode."EndedAt" IS NULL
                              AND episode."IsDeleted" = FALSE)) THEN
                        RAISE EXCEPTION 'Active coaching links require their matching open episode';
                    END IF;

                    RETURN NEW;
                END;
                $function$ LANGUAGE plpgsql;

                CREATE TRIGGER "TR_TrainerTraineeLinks_FenceEpisode"
                BEFORE INSERT OR UPDATE OR DELETE ON "TrainerTraineeLinks"
                FOR EACH ROW EXECUTE FUNCTION "FenceActiveCoachingEpisode"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Rolling back AddCoachingEpisodes is not supported because removing the additive schema would discard durable coaching relationship history.");
        }
    }
}
