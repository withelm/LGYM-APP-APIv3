using LgymApi.Application.Features.AdminManagement.Models;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Features.PasswordReset;
using LgymApi.Application.Features.PasswordReset.Contracts;
using LgymApi.Application.Models;
using LgymApi.Application.Pagination;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Services;
using LgymApi.TestUtils.Fakes;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class PasswordResetServiceTests
{
    [Test]
    public async Task RequestPasswordReset_WithValidEmail_CreatesTokenAndSchedulesEmail()
    {
        var user = CreateTestUser("test@example.com");
        var userRepository = new FakeUserRepository { ExistingByEmail = user };
        var tokenRepository = new FakePasswordResetTokenRepository();
        var emailScheduler = new FakePasswordRecoveryEmailScheduler();
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(userRepository, tokenRepository, emailScheduler, unitOfWork);

        using var cancellationSource = new CancellationTokenSource();
        var result = await service.RequestPasswordResetAsync("test@example.com", "en", cancellationSource.Token);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(tokenRepository.Added, Has.Exactly(1).Items);
        Assert.That(tokenRepository.Added[0].UserId, Is.EqualTo(user.Id));
        Assert.That(tokenRepository.Added[0].IsUsed, Is.False);
        Assert.That((tokenRepository.Added[0].ExpiresAt > DateTimeOffset.UtcNow), Is.True);
        Assert.That((tokenRepository.Added[0].ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(31)), Is.True);
        Assert.That(emailScheduler.ScheduledRequests, Has.Exactly(1).Items);
        var scheduledRequest = emailScheduler.ScheduledRequests[0];
        Assert.That(scheduledRequest.UserId, Is.EqualTo(user.Id));
        Assert.That(scheduledRequest.TokenId, Is.EqualTo(tokenRepository.Added[0].Id));
        Assert.That(scheduledRequest.UserName, Is.EqualTo(user.Name));
        Assert.That(scheduledRequest.RecipientEmail, Is.EqualTo("test@example.com"));
        Assert.That(scheduledRequest.ResetToken, Has.Length.EqualTo(64));
        Assert.That(scheduledRequest.ResetUrl, Is.Empty);
        Assert.That(scheduledRequest.CultureName, Is.EqualTo("en"));
        Assert.That(emailScheduler.CancellationToken, Is.EqualTo(cancellationSource.Token));
        var assertedValue140 = (typeof(PasswordResetService).GetConstructors()).ToArray();
        Assert.That(assertedValue140, Has.Exactly(1).Items);
        Assert.That(assertedValue140.Single().GetParameters()[4].ParameterType, Is.EqualTo(typeof(IPasswordRecoveryEmailScheduler)));
        Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task RequestPasswordReset_WithValidEmail_LeavesResetUrlEmpty()
    {
        var userRepository = new FakeUserRepository { ExistingByEmail = CreateTestUser("reset-url@example.com") };
        var tokenRepository = new FakePasswordResetTokenRepository();
        var emailScheduler = new FakePasswordRecoveryEmailScheduler();
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(userRepository, tokenRepository, emailScheduler, unitOfWork);

        var result = await service.RequestPasswordResetAsync("reset-url@example.com", "en-US", CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(emailScheduler.ScheduledRequests, Has.Exactly(1).Items);
        Assert.That(emailScheduler.ScheduledRequests[0].ResetUrl, Is.Empty);
    }

    [Test]
    public async Task RequestPasswordReset_WithNonexistentEmail_ReturnsSuccessWithoutCreatingToken()
    {
        var userRepository = new FakeUserRepository { ExistingByEmail = null };
        var tokenRepository = new FakePasswordResetTokenRepository();
        var emailScheduler = new FakePasswordRecoveryEmailScheduler();
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(userRepository, tokenRepository, emailScheduler, unitOfWork);

        var result = await service.RequestPasswordResetAsync("nonexistent@example.com", "en", CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(tokenRepository.Added, Is.Empty);
        Assert.That(emailScheduler.ScheduledRequests, Is.Empty);
        Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(0));
    }

    [Test]
    public async Task RequestPasswordReset_WithDeletedUser_ReturnsSuccessWithoutCreatingToken()
    {
        var user = CreateTestUser("deleted@example.com");
        user.IsDeleted = true;
        var userRepository = new FakeUserRepository { ExistingByEmail = user };
        var tokenRepository = new FakePasswordResetTokenRepository();
        var emailScheduler = new FakePasswordRecoveryEmailScheduler();
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(userRepository, tokenRepository, emailScheduler, unitOfWork);

        var result = await service.RequestPasswordResetAsync("deleted@example.com", "en", CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(tokenRepository.Added, Is.Empty);
        Assert.That(emailScheduler.ScheduledRequests, Is.Empty);
        Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(0));
    }

    [Test]
    public async Task RequestPasswordReset_WithExistingActiveToken_InvalidatesOldTokenAndCreatesNew()
    {
        var user = CreateTestUser("test@example.com");
        var oldToken = new PasswordResetToken
        {
            Id = Id<PasswordResetToken>.New(),
            UserId = user.Id,
            TokenHash = "oldtokenhash",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(20),
            IsUsed = false
        };
        var userRepository = new FakeUserRepository { ExistingByEmail = user };
        var tokenRepository = new FakePasswordResetTokenRepository { ExistingActiveForUser = [oldToken] };
        var emailScheduler = new FakePasswordRecoveryEmailScheduler();
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(userRepository, tokenRepository, emailScheduler, unitOfWork);

        var result = await service.RequestPasswordResetAsync("test@example.com", "en", CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(oldToken.IsUsed, Is.True);
        Assert.That(tokenRepository.Updated, Does.Contain(oldToken));
        Assert.That(tokenRepository.Added, Has.Exactly(1).Items);
        Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task RequestPasswordReset_NormalizesEmail_BeforeLookup()
    {
        var user = CreateTestUser("test@example.com");
        var userRepository = new FakeUserRepository { ExistingByEmail = user };
        var tokenRepository = new FakePasswordResetTokenRepository();
        var emailScheduler = new FakePasswordRecoveryEmailScheduler();
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(userRepository, tokenRepository, emailScheduler, unitOfWork);

        await service.RequestPasswordResetAsync("  TEST@EXAMPLE.COM  ", "en", CancellationToken.None);

        Assert.That(userRepository.LastEmailLookup, Is.EqualTo("test@example.com"));
    }

    [Test]
    public async Task RequestPasswordReset_StoresHashedToken_NotPlaintext()
    {
        var user = CreateTestUser("test@example.com");
        var userRepository = new FakeUserRepository { ExistingByEmail = user };
        var tokenRepository = new FakePasswordResetTokenRepository();
        var emailScheduler = new FakePasswordRecoveryEmailScheduler();
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(userRepository, tokenRepository, emailScheduler, unitOfWork);

        await service.RequestPasswordResetAsync("test@example.com", "en", CancellationToken.None);

        var addedToken = tokenRepository.Added[0];
        var scheduledRequest = emailScheduler.ScheduledRequests[0];

        Assert.That(addedToken.TokenHash, Is.Not.EqualTo(scheduledRequest.ResetToken));
        Assert.That(addedToken.TokenHash, Has.Length.EqualTo(64)); // SHA-256 hex output
        Assert.That(scheduledRequest.ResetToken, Has.Length.EqualTo(64)); // 32 bytes as hex
    }

    [Test]
    public async Task ResetPassword_WithValidToken_UpdatesPasswordAndMarksTokenUsed()
    {
        var user = CreateTestUser("test@example.com");
        var plainTextToken = "ABC123DEF456";
        var tokenHash = ComputeSha256Hex(plainTextToken);
        var resetToken = new PasswordResetToken
        {
            Id = Id<PasswordResetToken>.New(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(20),
            IsUsed = false
        };
        var userRepository = new FakeUserRepository { ExistingById = user };
        var tokenRepository = new FakePasswordResetTokenRepository { ExistingByTokenHash = resetToken };
        var unitOfWork = new FakeUnitOfWork();
        var sessionStore = new FakeUserSessionStore();
        var service = CreateService(userRepository, tokenRepository, unitOfWork: unitOfWork, sessionStore: sessionStore);

        var result = await service.ResetPasswordAsync(plainTextToken, "newPassword123", CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(user.LegacyHash, Is.Not.Empty);
        Assert.That(user.LegacySalt, Is.Not.Empty);
        Assert.That(resetToken.IsUsed, Is.True);
        Assert.That(tokenRepository.Updated, Does.Contain(resetToken));
        Assert.That(userRepository.Updated, Does.Contain(user));
        Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task ResetPassword_WithInvalidToken_ReturnsFailure()
    {
        var tokenRepository = new FakePasswordResetTokenRepository { ExistingByTokenHash = null };
        var userRepository = new FakeUserRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(userRepository, tokenRepository, unitOfWork: unitOfWork);

        var result = await service.ResetPasswordAsync("invalidtoken", "newPassword123", CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(0));
    }

    [Test]
    public async Task ResetPassword_WithExpiredToken_ReturnsFailure()
    {
        var user = CreateTestUser("test@example.com");
        var plainTextToken = "ABC123DEF456";
        var tokenHash = ComputeSha256Hex(plainTextToken);
        var expiredToken = new PasswordResetToken
        {
            Id = Id<PasswordResetToken>.New(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5), // Expired 5 minutes ago
            IsUsed = false
        };
        var tokenRepository = new FakePasswordResetTokenRepository { ExistingByTokenHash = null }; // FindActiveByTokenHashAsync filters expired tokens
        var userRepository = new FakeUserRepository { ExistingById = user };
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(userRepository, tokenRepository, unitOfWork: unitOfWork);

        var result = await service.ResetPasswordAsync(plainTextToken, "newPassword123", CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(0));
    }

    [Test]
    public async Task ResetPassword_WithUsedToken_ReturnsFailure()
    {
        var user = CreateTestUser("test@example.com");
        var plainTextToken = "ABC123DEF456";
        var tokenHash = ComputeSha256Hex(plainTextToken);
        var usedToken = new PasswordResetToken
        {
            Id = Id<PasswordResetToken>.New(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(20),
            IsUsed = true
        };
        var tokenRepository = new FakePasswordResetTokenRepository { ExistingByTokenHash = null }; // FindActiveByTokenHashAsync filters used tokens
        var userRepository = new FakeUserRepository { ExistingById = user };
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(userRepository, tokenRepository, unitOfWork: unitOfWork);

        var result = await service.ResetPasswordAsync(plainTextToken, "newPassword123", CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(0));
    }

    [Test]
    public async Task ResetPassword_WithDeletedUser_ReturnsFailure()
    {
        var user = CreateTestUser("test@example.com");
        user.IsDeleted = true;
        var plainTextToken = "ABC123DEF456";
        var tokenHash = ComputeSha256Hex(plainTextToken);
        var resetToken = new PasswordResetToken
        {
            Id = Id<PasswordResetToken>.New(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(20),
            IsUsed = false
        };
        var userRepository = new FakeUserRepository { ExistingById = user };
        var tokenRepository = new FakePasswordResetTokenRepository { ExistingByTokenHash = resetToken };
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(userRepository, tokenRepository, unitOfWork: unitOfWork);

        var result = await service.ResetPasswordAsync(plainTextToken, "newPassword123", CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(0));
    }

    [Test]
    public async Task ResetPassword_OnSuccess_RevokesAllUserSessions()
    {
        var user = CreateTestUser("test@example.com");
        var plainTextToken = "ABC123DEF456";
        var tokenHash = ComputeSha256Hex(plainTextToken);
        var resetToken = new PasswordResetToken
        {
            Id = Id<PasswordResetToken>.New(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(20),
            IsUsed = false
        };
        var userRepository = new FakeUserRepository { ExistingById = user };
        var tokenRepository = new FakePasswordResetTokenRepository { ExistingByTokenHash = resetToken };
        var unitOfWork = new FakeUnitOfWork();
        var sessionStore = new FakeUserSessionStore();
        var service = CreateService(userRepository, tokenRepository, unitOfWork: unitOfWork, sessionStore: sessionStore);

        await service.ResetPasswordAsync(plainTextToken, "newPassword123", CancellationToken.None);

        Assert.That(sessionStore.RevokedAllUserIds, Does.Contain(user.Id));
    }

    [Test]
    public async Task RequestPasswordReset_WhenPersistenceFails_DoesNotScheduleEmail()
    {
        var user = CreateTestUser("persistence-failure@example.com");
        var userRepository = new FakeUserRepository { ExistingByEmail = user };
        var tokenRepository = new FakePasswordResetTokenRepository();
        var emailScheduler = new FakePasswordRecoveryEmailScheduler();
        var persistenceFailure = new InvalidOperationException("Password reset persistence failed.");
        var unitOfWork = new FakeUnitOfWork { SaveChangesException = persistenceFailure };
        var service = CreateService(userRepository, tokenRepository, emailScheduler, unitOfWork);

        var action = () => service.RequestPasswordResetAsync(user.Email.Value, "en", CancellationToken.None);

        var exception = await Assert.CatchAsync<InvalidOperationException>(async () => { await action(); });
        Assert.That(exception, Is.SameAs(persistenceFailure));
        Assert.That(tokenRepository.Added, Has.Exactly(1).Items);
        Assert.That(emailScheduler.ScheduledRequests, Is.Empty);
        Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(1));
    }

    private static User CreateTestUser(string email)
    {
        return new User
        {
            Id = Id<User>.New(),
            Name = "Test User",
            Email = new Email(email),
            ProfileRank = "Rookie",
            IsDeleted = false,
            LegacyHash = string.Empty,
            LegacySalt = string.Empty
        };
    }

    private static string ComputeSha256Hex(string value)
    {
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
    }

    private static PasswordResetService CreateService(
        FakeUserRepository? userRepository = null,
        FakePasswordResetTokenRepository? tokenRepository = null,
        FakePasswordRecoveryEmailScheduler? emailScheduler = null,
        FakeUnitOfWork? unitOfWork = null,
        FakeUserSessionStore? sessionStore = null)
    {
        var actualTokenRepository = tokenRepository ?? new FakePasswordResetTokenRepository();

        return new PasswordResetService(
            userRepository ?? new FakeUserRepository(),
            actualTokenRepository,
            new PasswordResetTokenGenerationService(actualTokenRepository),
            new LegacyPasswordService(),
            emailScheduler ?? new FakePasswordRecoveryEmailScheduler(),
            sessionStore ?? new FakeUserSessionStore(),
            unitOfWork ?? new FakeUnitOfWork());
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public User? ExistingByEmail { get; set; }
        public User? ExistingById { get; set; }
        public string? LastEmailLookup { get; set; }
        public List<User> Updated { get; } = new();

        public Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default)
        {
            LastEmailLookup = email.Value;
            return Task.FromResult(ExistingByEmail);
        }

        public Task<User?> FindByIdAsync(Id<User> id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingById);
        }

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
        {
            Updated.Add(user);
            return Task.CompletedTask;
        }

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<User?> FindByIdIncludingDeletedAsync(Id<User> id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<User?> FindByIdWithRolesAsync(Id<User> id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<User?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<User?> FindByNameOrEmailAsync(string name, string email, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<UserRankingEntry>> GetRankingAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Pagination<UserResult>> GetUsersPaginatedAsync(FilterInput filterInput, bool includeDeleted, CancellationToken cancellationToken = default)
            => Task.FromResult(new Pagination<UserResult>());
    }

    private sealed class FakePasswordResetTokenRepository : IPasswordResetTokenRepository
    {
        public List<PasswordResetToken> Added { get; } = new();
        public List<PasswordResetToken> Updated { get; } = new();
        public PasswordResetToken? ExistingByTokenHash { get; set; }
        public List<PasswordResetToken> ExistingActiveForUser { get; set; } = new();

        public Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken = default)
        {
            Added.Add(token);
            return Task.CompletedTask;
        }

        public Task<PasswordResetToken?> FindActiveByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingByTokenHash);
        }

        public Task<List<PasswordResetToken>> GetActiveForUserAsync(Id<User> userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingActiveForUser);
        }

        public Task UpdateAsync(PasswordResetToken token, CancellationToken cancellationToken = default)
        {
            Updated.Add(token);
            return Task.CompletedTask;
        }

        public Task<bool> TokenHashExistsAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }
    }

    private sealed class FakePasswordRecoveryEmailScheduler : IPasswordRecoveryEmailScheduler
    {
        public List<PasswordRecoveryEmailRequest> ScheduledRequests { get; } = new();
        public CancellationToken CancellationToken { get; private set; }

        public Task ScheduleAsync(PasswordRecoveryEmailRequest request, CancellationToken cancellationToken = default)
        {
            ScheduledRequests.Add(request);
            CancellationToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }
        public Exception? SaveChangesException { get; init; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls += 1;
            return SaveChangesException is null
                ? Task.FromResult(1)
                : Task.FromException<int>(SaveChangesException);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public void DetachEntity<TEntity>(TEntity entity) where TEntity : class
            => throw new NotSupportedException();
    }
}
