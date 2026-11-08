using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.IntegrationTests;

// Historical migration fixtures must not INSERT columns from the current EF model.
internal static class PostgreSqlHistoricalUserSeed
{
    internal static async Task AddAsync(AppDbContext context, params User[] users)
    {
        foreach (var user in users)
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Users" ("Id", "Name", "Email", "PreferredLanguage", "PreferredTimeZone", "ProfileRank", "IsVisibleInRanking", "IsBlocked", "IsDeleted", "CreatedAt", "UpdatedAt")
                VALUES (CAST({user.Id.ToString()} AS uuid), {user.Name}, {user.Email.Value}, {user.PreferredLanguage}, {user.PreferredTimeZone}, {user.ProfileRank}, {user.IsVisibleInRanking}, {user.IsBlocked}, {user.IsDeleted}, {user.CreatedAt}, {user.UpdatedAt})
                """);
        }
    }
}
