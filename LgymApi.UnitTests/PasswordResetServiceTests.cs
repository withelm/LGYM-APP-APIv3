using LgymApi.Application.Features.AdminManagement.Models;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.PasswordReset;
using LgymApi.Application.Models;
using LgymApi.Application.Pagination;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Services;

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

        var result = await service.RequestPasswordResetAsync("test@example.com", "en", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(tokenRepository.Added, Has.Count.EqualTo(1));
            Assert.That(tokenRepository.Added[0].UserId, Is.EqualTo(user.Id));
            Assert.That(tokenRepository.Added[0].IsUsed, Is.False);
            Assert.That(tokenRepository.Added[0].ExpiresAt, Is.GreaterThan(DateTimeOffset.UtcNow).And.LessThan(DateTimeOffset.UtcNow.AddMinutes(31)));
            Assert.That(emailScheduler.ScheduledPayloads, Has.Count.EqualTo(1));
            Assert.That(emailScheduler.ScheduledPayloads[0].UserId, Is.EqualTo(user.Id));
            Assert.That(emailScheduler.ScheduledPayloads[0].RecipientEmail, Is.EqualTo("test@example.com"));
            Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(1));
        });
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

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(tokenRepository.Added, Is.Empty);
            Assert.That(emailScheduler.ScheduledPayloads, Is.Empty);
            Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(0));
        });
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

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(tokenRepository.Added, Is.Empty);
            Assert.That(emailScheduler.ScheduledPayloads, Is.Empty);
            Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(0));
        });
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

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(oldToken.IsUsed, Is.True);
            Assert.That(tokenRepository.Updated, Contains.Item(oldToken));
            Assert.That(tokenRepository.Added, Has.Count.EqualTo(1));
            Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(1));
        });
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
        var scheduledPayload = emailScheduler.ScheduledPayloads[0];

        Assert.Multiple(() =>
        {
            Assert.That(addedToken.TokenHash, Is.Not.EqualTo(scheduledPayload.ResetToken));
            Assert.That(addedToken.TokenHash, Has.Length.EqualTo(64)); // SHA-256 hex output
            Assert.That(scheduledPayload.ResetToken, Has.Length.EqualTo(64)); // 32 bytes as hex
        });
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
        var sessionCache = new FakeUserSessionCache();
        var service = CreateService(userRepository, tokenRepository, unitOfWork: unitOfWork, sessionCache: sessionCache);

        var result = await service.ResetPasswordAsync(plainTextToken, "newPassword123", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(user.LegacyHash, Is.Not.Empty);
            Assert.That(user.LegacySalt, Is.Not.Empty);
            Assert.That(resetToken.IsUsed, Is.True);
            Assert.That(tokenRepository.Updated, Contains.Item(resetToken));
            Assert.That(userRepository.Updated, Contains.Item(user));
            Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ResetPassword_WithInvalidToken_ReturnsFailure()
    {
        var tokenRepository = new FakePasswordResetTokenRepository { ExistingByTokenHash = null };
        var userRepository = new FakeUserRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(userRepository, tokenRepository, unitOfWork: unitOfWork);

        var result = await service.ResetPasswordAsync("invalidtoken", "newPassword123", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(0));
        });
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

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(0));
        });
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

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(0));
        });
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

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task ResetPassword_OnSuccess_InvalidatesUserSessionCache()
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
        var sessionCache = new FakeUserSessionCache();
        var service = CreateService(userRepository, tokenRepository, unitOfWork: unitOfWork, sessionCache: sessionCache);

        await service.ResetPasswordAsync(plainTextToken, "newPassword123", CancellationToken.None);

        Assert.That(sessionCache.RemovedUserIds, Contains.Item(user.Id));
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
         FakeUserSessionCache? sessionCache = null)
     {
         var actualTokenRepository = tokenRepository ?? new FakePasswordResetTokenRepository();
         var dependencies = new FakePasswordResetServiceDependencies(
             userRepository ?? new FakeUserRepository(),
             actualTokenRepository,
             new PasswordResetTokenGenerationService(actualTokenRepository),
             new LegacyPasswordService(),
             emailScheduler ?? new FakePasswordRecoveryEmailScheduler(),
             sessionCache ?? new FakeUserSessionCache(),
             unitOfWork ?? new FakeUnitOfWork());

         return new PasswordResetService(dependencies);
     }

    private sealed class FakePasswordResetServiceDependencies : IPasswordResetServiceDependencies
    {
        public FakePasswordResetServiceDependencies(
            IUserRepository userRepository,
            IPasswordResetTokenRepository passwordResetTokenRepository,
            IPasswordResetTokenGenerationService tokenGenerationService,
            ILegacyPasswordService legacyPasswordService,
            IEmailScheduler<PasswordRecoveryEmailPayload> passwordRecoveryEmailScheduler,
            IUserSessionCache userSessionCache,
            IUnitOfWork unitOfWork)
        {
            UserRepository = userRepository;
            PasswordResetTokenRepository = passwordResetTokenRepository;
            TokenGenerationService = tokenGenerationService;
            LegacyPasswordService = legacyPasswordService;
            PasswordRecoveryEmailScheduler = passwordRecoveryEmailScheduler;
            UserSessionCache = userSessionCache;
            UnitOfWork = unitOfWork;
        }

        public IUserRepository UserRepository { get; }
        public IPasswordResetTokenRepository PasswordResetTokenRepository { get; }
        public IPasswordResetTokenGenerationService TokenGenerationService { get; }
        public ILegacyPasswordService LegacyPasswordService { get; }
        public IEmailScheduler<PasswordRecoveryEmailPayload> PasswordRecoveryEmailScheduler { get; }
        public IUserSessionCache UserSessionCache { get; }
        public IUnitOfWork UnitOfWork { get; }
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

    private sealed class FakePasswordRecoveryEmailScheduler : IEmailScheduler<PasswordRecoveryEmailPayload>
    {
        public List<PasswordRecoveryEmailPayload> ScheduledPayloads { get; } = new();

        public Task ScheduleAsync(PasswordRecoveryEmailPayload payload, CancellationToken cancellationToken = default)
        {
            ScheduledPayloads.Add(payload);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls += 1;
            return Task.FromResult(1);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public void DetachEntity<TEntity>(TEntity entity) where TEntity : class
            => throw new NotSupportedException();
    }

    private sealed class FakeUserSessionCache : IUserSessionCache
    {
        public List<Id<User>> RemovedUserIds { get; } = new();
        public int Count => 0;

        public bool Remove(Id<User> userId)
        {
            RemovedUserIds.Add(userId);
            return true;
        }

        public void AddOrRefresh(Id<User> userId)
            => throw new NotSupportedException();

        public bool Contains(Id<User> userId)
            => throw new NotSupportedException();
    }
}
