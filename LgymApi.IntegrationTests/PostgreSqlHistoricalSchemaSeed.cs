using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.IntegrationTests;

/// <summary>
/// Seeds rows for fixtures that stop at a historical migration target. The current
/// <see cref="AppDbContext"/> model always writes every mapped column, so seeding through EF
/// fails once the model gains columns that the historical schema does not have yet.
/// </summary>
internal static class PostgreSqlHistoricalSchemaSeed
{
    /// <summary>
    /// Inserts a <c>Users</c> row using only the columns that exist in every schema at or after
    /// the migration targets used by the historical fixtures.
    /// </summary>
    public static async Task<Id<User>> InsertUserAsync(AppDbContext dbContext, string name, string email)
    {
        var id = Id<User>.New();
        var now = DateTimeOffset.UtcNow;

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "Users" (
                "Id", "Name", "Email", "ProfileRank", "PreferredLanguage", "PreferredTimeZone",
                "IsVisibleInRanking", "IsBlocked", "IsDeleted", "CreatedAt", "UpdatedAt")
            VALUES (CAST({0} AS uuid), {1}, {2}, 'Rookie', '', '', TRUE, FALSE, FALSE, {3}, {3});
            """,
            id.ToString(),
            name,
            email,
            now);

        return id;
    }
}
