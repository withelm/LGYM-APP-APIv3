using LgymApi.Application.Services;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.TestUtils.Fakes
{
    public class FakeUserSessionStore : IUserSessionStore
    {
        private readonly Dictionary<Id<UserSession>, UserSession> _sessions = new();

        public List<Id<UserSession>> RevokedSessionIds { get; } = new();
        public List<Id<User>> RevokedAllUserIds { get; } = new();

        public Task<UserSession> CreateSessionAsync(Id<User> userId, DateTimeOffset expiresAtUtc, CancellationToken ct)
        {
            var session = new UserSession
            {
                Id = Id<UserSession>.New(),
                UserId = userId,
                Jti = Id<UserSession>.New().ToString(),
                ExpiresAtUtc = expiresAtUtc,
                RevokedAtUtc = null
            };

            _sessions[session.Id] = session;
            return Task.FromResult(session);
        }

        public Task<bool> ValidateSessionAsync(Id<UserSession> sessionId, CancellationToken ct)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                return Task.FromResult(false);
            }

            var isValid = session.RevokedAtUtc == null
                          && session.ExpiresAtUtc > DateTimeOffset.UtcNow
                          && !session.IsDeleted;

            return Task.FromResult(isValid);
        }

        public Task RevokeSessionAsync(Id<UserSession> sessionId, CancellationToken ct)
        {
            RevokedSessionIds.Add(sessionId);

            if (_sessions.TryGetValue(sessionId, out var session))
            {
                session.RevokedAtUtc = DateTimeOffset.UtcNow;
            }

            return Task.CompletedTask;
        }

        public Task RevokeAllUserSessionsAsync(Id<User> userId, CancellationToken ct)
        {
            RevokedAllUserIds.Add(userId);

            foreach (var session in _sessions.Values.Where(s => s.UserId == userId))
            {
                session.RevokedAtUtc = DateTimeOffset.UtcNow;
            }

            return Task.CompletedTask;
        }
    }
}

namespace LgymApi.UnitTests.Fakes
{
    public sealed class FakeUserSessionStore : global::LgymApi.TestUtils.Fakes.FakeUserSessionStore
    {
    }
}
