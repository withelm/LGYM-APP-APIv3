using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class UserSessionStoreTests
{
    private static DbContextOptions<AppDbContext> CreateOptions(string databaseName)
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
    }

    private static async Task<AppDbContext> CreateDbContextAsync(string databaseName)
    {
        var dbContext = new AppDbContext(CreateOptions(databaseName));
        await dbContext.Database.EnsureCreatedAsync();
        return dbContext;
    }

    private static async Task SeedUserAsync(AppDbContext dbContext, Id<User> userId, string name = "testuser", string email = "test@example.com")
    {
        dbContext.Users.Add(new User
        {
            Id = userId,
            Name = name,
            Email = new Email(email),
            ProfileRank = "Rookie"
        });

        await dbContext.SaveChangesAsync();
    }

    [Test]
    public async Task CreateSessionAsync_AddsEntityToDbContext()
    {
        var databaseName = $"lgymunittest_{Id<UserSessionStoreTests>.New():N}";
        await using var dbContext = await CreateDbContextAsync(databaseName);

        var userId = Id<User>.New();
        await SeedUserAsync(dbContext, userId);

        var store = new UserSessionStore(dbContext);
        var expiresAt = DateTimeOffset.UtcNow.AddDays(30);

        var session = await store.CreateSessionAsync(userId, expiresAt, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        session.UserId.Should().Be(userId);
        session.ExpiresAtUtc.Should().Be(expiresAt);
        session.Jti.Should().NotBeEmpty();
        session.RevokedAtUtc.Should().BeNull();

        var retrieved = await dbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
        retrieved.Should().NotBeNull();
    }

    [Test]
    public async Task ValidateSessionAsync_ReturnsTrueForActiveSession()
    {
        var databaseName = $"lgymunittest_{Id<UserSessionStoreTests>.New():N}";
        await using var dbContext = await CreateDbContextAsync(databaseName);

        var userId = Id<User>.New();
        await SeedUserAsync(dbContext, userId);

        var session = new UserSession
        {
            Id = Id<UserSession>.New(),
            UserId = userId,
            Jti = Id<UserSession>.New().ToString(),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(30),
            RevokedAtUtc = null
        };
        dbContext.UserSessions.Add(session);
        await dbContext.SaveChangesAsync();

        var store = new UserSessionStore(dbContext);
        var isValid = await store.ValidateSessionAsync(session.Id, CancellationToken.None);

        isValid.Should().BeTrue();
    }

    [Test]
    public async Task ValidateSessionAsync_ReturnsFalseForRevokedSession()
    {
        var databaseName = $"lgymunittest_{Id<UserSessionStoreTests>.New():N}";
        await using var dbContext = await CreateDbContextAsync(databaseName);

        var userId = Id<User>.New();
        await SeedUserAsync(dbContext, userId);

        var session = new UserSession
        {
            Id = Id<UserSession>.New(),
            UserId = userId,
            Jti = Id<UserSession>.New().ToString(),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(30),
            RevokedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        dbContext.UserSessions.Add(session);
        await dbContext.SaveChangesAsync();

        var store = new UserSessionStore(dbContext);
        var isValid = await store.ValidateSessionAsync(session.Id, CancellationToken.None);

        isValid.Should().BeFalse();
    }

    [Test]
    public async Task ValidateSessionAsync_ReturnsFalseForExpiredSession()
    {
        var databaseName = $"lgymunittest_{Id<UserSessionStoreTests>.New():N}";
        await using var dbContext = await CreateDbContextAsync(databaseName);

        var userId = Id<User>.New();
        await SeedUserAsync(dbContext, userId);

        var session = new UserSession
        {
            Id = Id<UserSession>.New(),
            UserId = userId,
            Jti = Id<UserSession>.New().ToString(),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            RevokedAtUtc = null
        };
        dbContext.UserSessions.Add(session);
        await dbContext.SaveChangesAsync();

        var store = new UserSessionStore(dbContext);
        var isValid = await store.ValidateSessionAsync(session.Id, CancellationToken.None);

        isValid.Should().BeFalse();
    }

    [Test]
    public async Task ValidateSessionAsync_ReturnsFalseForNonExistentSession()
    {
        var databaseName = $"lgymunittest_{Id<UserSessionStoreTests>.New():N}";
        await using var dbContext = await CreateDbContextAsync(databaseName);

        var store = new UserSessionStore(dbContext);
        var nonExistentSessionId = Id<UserSession>.New();

        var isValid = await store.ValidateSessionAsync(nonExistentSessionId, CancellationToken.None);

        isValid.Should().BeFalse();
    }

    [Test]
    public async Task RevokeSessionAsync_SetsRevokedAtUtc()
    {
        var databaseName = $"lgymunittest_{Id<UserSessionStoreTests>.New():N}";
        await using var dbContext = await CreateDbContextAsync(databaseName);

        var userId = Id<User>.New();
        await SeedUserAsync(dbContext, userId);

        var session = new UserSession
        {
            Id = Id<UserSession>.New(),
            UserId = userId,
            Jti = Id<UserSession>.New().ToString(),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(30),
            RevokedAtUtc = null
        };
        dbContext.UserSessions.Add(session);
        await dbContext.SaveChangesAsync();

        var store = new UserSessionStore(dbContext);
        await store.RevokeSessionAsync(session.Id, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var retrieved = await dbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
        retrieved!.RevokedAtUtc.Should().NotBeNull();
    }

    [Test]
    public async Task RevokeAllUserSessionsAsync_RevokesAllActiveSessions()
    {
        var databaseName = $"lgymunittest_{Id<UserSessionStoreTests>.New():N}";
        var options = CreateOptions(databaseName);

        await using var setupContext = new AppDbContext(options);
        await setupContext.Database.EnsureCreatedAsync();

        var userId = Id<User>.New();
        await SeedUserAsync(setupContext, userId);

        var session1 = new UserSession
        {
            Id = Id<UserSession>.New(),
            UserId = userId,
            Jti = Id<UserSession>.New().ToString(),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(30),
            RevokedAtUtc = null
        };
        var session2 = new UserSession
        {
            Id = Id<UserSession>.New(),
            UserId = userId,
            Jti = Id<UserSession>.New().ToString(),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(30),
            RevokedAtUtc = null
        };
        var alreadyRevokedSession = new UserSession
        {
            Id = Id<UserSession>.New(),
            UserId = userId,
            Jti = Id<UserSession>.New().ToString(),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(30),
            RevokedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
        };

        setupContext.UserSessions.AddRange(session1, session2, alreadyRevokedSession);
        await setupContext.SaveChangesAsync();

        var session1Id = session1.Id;
        var session2Id = session2.Id;
        var alreadyRevokedId = alreadyRevokedSession.Id;

        await using var storeContext = new AppDbContext(options);
        var store = new UserSessionStore(storeContext);
        await store.RevokeAllUserSessionsAsync(userId, CancellationToken.None);
        await storeContext.SaveChangesAsync();

        await using var verifyContext = new AppDbContext(options);
        var allSessions = await verifyContext.UserSessions.Where(s => s.UserId == userId).ToListAsync();
        var activeRevoked = allSessions.Where(s => s.Id == session1Id || s.Id == session2Id).ToList();
        var stillRevoked = allSessions.FirstOrDefault(s => s.Id == alreadyRevokedId);

        activeRevoked.Should().HaveCount(2);
        activeRevoked.All(s => s.RevokedAtUtc != null).Should().BeTrue();
        stillRevoked!.RevokedAtUtc.Should().NotBeNull();
    }

    [Test]
    public async Task RevokeAllUserSessionsAsync_DoesNotAffectOtherUsers()
    {
        var databaseName = $"lgymunittest_{Id<UserSessionStoreTests>.New():N}";
        var options = CreateOptions(databaseName);

        await using var setupContext = new AppDbContext(options);
        await setupContext.Database.EnsureCreatedAsync();

        var user1Id = Id<User>.New();
        var user2Id = Id<User>.New();

        await SeedUserAsync(setupContext, user1Id, "user1", "user1@example.com");
        await SeedUserAsync(setupContext, user2Id, "user2", "user2@example.com");

        var user1Session = new UserSession
        {
            Id = Id<UserSession>.New(),
            UserId = user1Id,
            Jti = Id<UserSession>.New().ToString(),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(30),
            RevokedAtUtc = null
        };
        var user2Session = new UserSession
        {
            Id = Id<UserSession>.New(),
            UserId = user2Id,
            Jti = Id<UserSession>.New().ToString(),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(30),
            RevokedAtUtc = null
        };

        setupContext.UserSessions.AddRange(user1Session, user2Session);
        await setupContext.SaveChangesAsync();

        var user1SessionId = user1Session.Id;
        var user2SessionId = user2Session.Id;

        await using var storeContext = new AppDbContext(options);
        var store = new UserSessionStore(storeContext);
        await store.RevokeAllUserSessionsAsync(user1Id, CancellationToken.None);
        await storeContext.SaveChangesAsync();

        await using var verifyContext = new AppDbContext(options);
        var user1Retrieved = await verifyContext.UserSessions.FirstOrDefaultAsync(s => s.Id == user1SessionId);
        var user2Retrieved = await verifyContext.UserSessions.FirstOrDefaultAsync(s => s.Id == user2SessionId);

        user1Retrieved!.RevokedAtUtc.Should().NotBeNull();
        user2Retrieved!.RevokedAtUtc.Should().BeNull();
    }
}
