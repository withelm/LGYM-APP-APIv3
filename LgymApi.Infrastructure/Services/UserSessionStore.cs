using LgymApi.Application.Services;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Services;

public sealed class UserSessionStore : IUserSessionStore
{
    private readonly AppDbContext _dbContext;

    public UserSessionStore(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserSession> CreateSessionAsync(Id<User> userId, DateTimeOffset expiresAtUtc, CancellationToken ct)
    {
        var session = new UserSession
        {
            Id = Id<UserSession>.New(),
            UserId = userId,
            Jti = Id<UserSession>.New().ToString(),
            ExpiresAtUtc = expiresAtUtc
        };

        await _dbContext.UserSessions.AddAsync(session, ct);
        return session;
    }

    public Task<bool> ValidateSessionAsync(Id<UserSession> sessionId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        return _dbContext.UserSessions
            .AsNoTracking()
            .AnyAsync(
                session => session.Id == sessionId
                    && session.RevokedAtUtc == null
                    && session.ExpiresAtUtc > now
                    && !session.IsDeleted,
                ct);
    }

    public async Task RevokeSessionAsync(Id<UserSession> sessionId, CancellationToken ct)
    {
        var session = await _dbContext.UserSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && !s.IsDeleted, ct);

        if (session is null)
        {
            return;
        }

        session.RevokedAtUtc = DateTimeOffset.UtcNow;
    }

    public Task RevokeAllUserSessionsAsync(Id<User> userId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        return _dbContext.UserSessions
            .Where(session => session.UserId == userId
                && session.RevokedAtUtc == null
                && session.ExpiresAtUtc > now
                && !session.IsDeleted)
            .StageUpdateAsync(
                _dbContext,
                session => session.RevokedAtUtc,
                _ => (DateTimeOffset?)now,
                ct);
    }
}
