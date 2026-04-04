using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.DataSeeder.Seeders;

public sealed class PasswordResetTokenSeeder : IEntitySeeder
{
    public int Order => 5;

    public async Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Start("password reset tokens");

        var user = seedContext.AdminUser ?? seedContext.TesterUser;
        if (user == null)
        {
            SeedOperationConsole.Skip("password reset tokens");
            return;
        }

        var tokenHash = $"SEED_RESET_{user.Id}";
        var exists = await context.PasswordResetTokens
            .AsNoTracking()
            .AnyAsync(token => token.UserId == user.Id || token.TokenHash == tokenHash, cancellationToken);

        if (exists)
        {
            SeedOperationConsole.Skip("password reset tokens");
            return;
        }

        var tokenEntity = new PasswordResetToken
        {
            Id = Id<PasswordResetToken>.New(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1),
            IsUsed = true
        };

        await context.PasswordResetTokens.AddAsync(tokenEntity, cancellationToken);
        SeedOperationConsole.Done("password reset tokens");
    }
}
