using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Identity.Errors;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Notifications.Repositories;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Infrastructure;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories;
using LgymApi.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Npgsql;
using LgymApi.UnitTests.Fakes;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class PushInstallationLifecycleServiceTests
{
    private ConfigurablePushInstallationRepository _pushInstallationRepository = null!;
    private ConfigurableConflictClassifier _conflictClassifier = null!;
    private IUnitOfWork _unitOfWork = null!;
    private PushInstallationLifecycleService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _pushInstallationRepository = new ConfigurablePushInstallationRepository();
        _conflictClassifier = new ConfigurableConflictClassifier();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _service = new PushInstallationLifecycleService(_pushInstallationRepository, _conflictClassifier, _unitOfWork);
    }

    [Test]
    public async Task RegisterAsync_NormalizesValues_StagesRegistrationAndCommitsOnce()
    {
        var userId = CreateUserId();
        var sessionId = Id<UserSession>.New();
        var beforeRegistration = DateTimeOffset.UtcNow;

        var result = await _service.RegisterAsync(
            userId,
            sessionId,
            new RegisterPushInstallationInput(" device-1 ", " ios ", " token-1 ", " 2.0.0 ", " production ", " authorized "));
        var afterRegistration = DateTimeOffset.UtcNow;

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(_pushInstallationRepository.Calls
            .Where(call =>
                call.Method == nameof(IPushInstallationRepository.UpsertForUserSessionAsync)
                && call.Argument is PushInstallationRegistration registration
                && registration.InstallationId == "device-1"
                && registration.Platform == "ios"
                && registration.FcmToken == "token-1"
                && registration.AppVersion == "2.0.0"
                && registration.Environment == "production"
                && registration.PermissionStatus == "authorized"
                && registration.UserId == userId
                && registration.SessionId == sessionId
                && registration.LastSeenAt.Offset == TimeSpan.Zero
                && registration.LastSeenAt >= beforeRegistration
                && registration.LastSeenAt <= afterRegistration), Has.Exactly(1).Items);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [TestCase(null, null)]
    [TestCase("", "")]
    [TestCase(" \t ", " \t ")]
    public async Task RegisterAsync_WhenOptionalValuesAreBlank_StagesNullOptionalValues(
        string? appVersion,
        string? permissionStatus)
    {
        var result = await _service.RegisterAsync(
            CreateUserId(),
            Id<UserSession>.New(),
            new RegisterPushInstallationInput("device-1", "android", "token-1", appVersion, "development", permissionStatus));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(_pushInstallationRepository.Calls
            .Where(call =>
                call.Method == nameof(IPushInstallationRepository.UpsertForUserSessionAsync)
                && call.Argument is PushInstallationRegistration registration
                && registration.AppVersion == null
                && registration.PermissionStatus == null), Has.Exactly(1).Items);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RegisterAsync_WhenFcmTokenUniqueConstraintLosesRace_ReturnsForbiddenWithoutLeakingToken()
    {
        const string token = "secret-racing-token";
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(CreateUniqueViolation("IX_PushInstallations_FcmToken")));
        _conflictClassifier.ShouldClassify = true;

        var result = await _service.RegisterAsync(
            CreateUserId(),
            Id<UserSession>.New(),
            new RegisterPushInstallationInput("device-race", "android", token, null, "production", null));

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<ForbiddenError>());
        var assertedChain45 = result.Error.Message;
        Assert.That(assertedChain45, Is.EqualTo(Messages.Forbidden));
        Assert.That(assertedChain45, Does.Not.Contain(token));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RegisterAsync_WhenInstallationIdUniqueConstraintLosesRace_ReturnsForbidden()
    {
        var persistenceFailure = CreateUniqueViolation("IX_PushInstallations_InstallationId");
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(persistenceFailure));
        _conflictClassifier.ShouldClassify = true;

        var result = await _service.RegisterAsync(
            CreateUserId(),
            Id<UserSession>.New(),
            new RegisterPushInstallationInput("device-race", "android", "token-race", null, "production", null));

        Assert.That(result.Error, Is.TypeOf<ForbiddenError>());
        Assert.That((_pushInstallationRepository.Calls).Where(call => call.Method == nameof(IPushInstallationRepository.ResetFailedMutationAsync)), Has.Exactly(1).Items);
    }

    [Test]
    public async Task RegisterAsync_WhenUnrelatedPersistenceFailureOccurs_Propagates()
    {
        var persistenceFailure = CreateUniqueViolation("IX_Unrelated");
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(persistenceFailure));
        _conflictClassifier.ShouldClassify = false;

        var action = () => _service.RegisterAsync(
            CreateUserId(),
            Id<UserSession>.New(),
            new RegisterPushInstallationInput("device-race", "android", "token-race", null, "production", null));

        var exception = await Assert.CatchAsync<DbUpdateException>(async () => { await action(); });
        Assert.That(exception, Is.SameAs(persistenceFailure));
        Assert.That((_pushInstallationRepository.Calls).Any(call => call.Method == nameof(IPushInstallationRepository.ResetFailedMutationAsync)), Is.False);
    }

    [TestCase("IX_PushInstallations_FcmToken", true)]
    [TestCase("IX_PushInstallations_InstallationId", true)]
    [TestCase("IX_Unrelated", false)]
    public void RegistrationConflictClassifier_RecognizesOnlyOwnedUniqueConstraints(
        string constraintName,
        bool expected)
    {
        var services = new ServiceCollection();
        services.AddNotificationsInfrastructure();
        using var provider = services.BuildServiceProvider();
        var classifier = provider.GetRequiredService<IPushInstallationPersistenceConflictClassifier>();

        Assert.That(classifier.IsRegistrationConflict(CreateUniqueViolation(constraintName)), Is.EqualTo(expected));
    }

    [Test]
    public async Task RegisterAsync_WhenOptimisticConcurrencyLosesRace_ReturnsForbiddenAndResetsOnlyInstallation()
    {
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(new DbUpdateConcurrencyException("stale registration")));

        var result = await _service.RegisterAsync(
            CreateUserId(),
            Id<UserSession>.New(),
            new RegisterPushInstallationInput("device-race", "android", "token-race", null, "production", null));

        Assert.That(result.Error, Is.TypeOf<ForbiddenError>());
        Assert.That((_pushInstallationRepository.Calls).Where(call => call.Method == nameof(IPushInstallationRepository.ResetFailedMutationAsync)), Has.Exactly(1).Items);
    }

    [Test]
    public async Task UnregisterAsync_WhenOptimisticConcurrencyLosesRace_ReturnsForbiddenAndResetsOnlyInstallation()
    {
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(new DbUpdateConcurrencyException("stale unregister")));

        var result = await _service.UnregisterAsync(
            CreateUserId(),
            Id<UserSession>.New(),
            new PushInstallationActionInput("device-race"));

        Assert.That(result.Error, Is.TypeOf<ForbiddenError>());
        Assert.That((_pushInstallationRepository.Calls).Where(call => call.Method == nameof(IPushInstallationRepository.ResetFailedMutationAsync)), Has.Exactly(1).Items);
    }

    [Test]
    public async Task RegisterAsync_WhenConflictModifiesExistingInstallation_CleansTrackedMutationBeforeLaterSave()
    {
        await using var database = CreateDatabase();
        var installation = CreateInstallation("existing-device", "existing-token");
        database.PushInstallations.Add(installation);
        await database.SaveChangesAsync();
        var unitOfWork = new FailOnceUnitOfWork(database, CreateUniqueViolation("IX_PushInstallations_FcmToken"));
        var classifier = new ConfigurableConflictClassifier { ShouldClassify = true };
        var service = new PushInstallationLifecycleService(new PushInstallationRepository(database), classifier, unitOfWork);

        var result = await service.RegisterAsync(
            CreateUserId(),
            Id<UserSession>.New(),
            new RegisterPushInstallationInput("existing-device", "android", "conflicting-token", null, "production", null));
        await unitOfWork.SaveChangesAsync();
        database.ChangeTracker.Clear();
        var persisted = await database.PushInstallations.SingleAsync();

        Assert.That(result.Error, Is.TypeOf<ForbiddenError>());
        Assert.That(persisted.UserId, Is.Null);
        Assert.That(persisted.SessionId, Is.Null);
        Assert.That(persisted.FcmToken, Is.EqualTo("existing-token"));
    }

    [Test]
    public async Task RegisterAsync_WhenConflictAddsInstallation_DetachesTrackedInsertBeforeLaterSave()
    {
        await using var database = CreateDatabase();
        var unitOfWork = new FailOnceUnitOfWork(database, CreateUniqueViolation("IX_PushInstallations_InstallationId"));
        var classifier = new ConfigurableConflictClassifier { ShouldClassify = true };
        var service = new PushInstallationLifecycleService(new PushInstallationRepository(database), classifier, unitOfWork);

        var result = await service.RegisterAsync(
            CreateUserId(),
            Id<UserSession>.New(),
            new RegisterPushInstallationInput("new-device", "android", "new-token", null, "production", null));
        await unitOfWork.SaveChangesAsync();

        Assert.That(result.Error, Is.TypeOf<ForbiddenError>());
        Assert.That((await database.PushInstallations.CountAsync()), Is.EqualTo(0));
    }

    [Test]
    public async Task RegisterAsync_WhenUnauthenticated_DoesNotWriteOrCommit()
    {
        var result = await _service.RegisterAsync(
            null,
            Id<UserSession>.New(),
            new RegisterPushInstallationInput("device-1", "android", "token-1", null, "development", null));

        Assert.That(result.Error, Is.TypeOf<UserUnauthorizedError>());
        Assert.That(result.Error.Message, Is.EqualTo(Messages.Unauthorized));
        Assert.That((_pushInstallationRepository.Calls).Any(call => call.Method == nameof(IPushInstallationRepository.UpsertForUserSessionAsync)), Is.False);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RegisterAsync_WhenSessionIsMissing_DoesNotWriteOrCommit()
    {
        var result = await _service.RegisterAsync(
            CreateUserId(),
            null,
            new RegisterPushInstallationInput("device-1", "android", "token-1", null, "development", null));

        Assert.That(result.Error, Is.TypeOf<UserUnauthorizedError>());
        Assert.That(result.Error.Message, Is.EqualTo(Messages.Unauthorized));
        Assert.That((_pushInstallationRepository.Calls).Any(call => call.Method == nameof(IPushInstallationRepository.UpsertForUserSessionAsync)), Is.False);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [TestCase("", "android", "token-1", "development")]
    [TestCase("device-1", "", "token-1", "development")]
    [TestCase("device-1", "android", "", "development")]
    [TestCase("device-1", "android", "token-1", "")]
    [TestCase(null, "android", "token-1", "development")]
    [TestCase("device-1", null, "token-1", "development")]
    [TestCase("device-1", "android", null, "development")]
    [TestCase("device-1", "android", "token-1", null)]
    [TestCase(" ", "android", "token-1", "development")]
    [TestCase("device-1", " ", "token-1", "development")]
    [TestCase("device-1", "android", " ", "development")]
    [TestCase("device-1", "android", "token-1", " ")]
    public async Task RegisterAsync_WhenRequiredValueIsBlank_DoesNotWriteOrCommit(
        string? installationKey,
        string? platform,
        string? fcmToken,
        string? environment)
    {
        var result = await _service.RegisterAsync(
            CreateUserId(),
            Id<UserSession>.New(),
            new RegisterPushInstallationInput(installationKey!, platform!, fcmToken!, null, environment!, null));

        Assert.That(result.Error, Is.TypeOf<InvalidUserError>());
        Assert.That(result.Error.Message, Is.EqualTo(Messages.FieldRequired));
        Assert.That((_pushInstallationRepository.Calls).Any(call => call.Method == nameof(IPushInstallationRepository.UpsertForUserSessionAsync)), Is.False);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UnregisterAsync_StagesUnregisteredActionAndCommitsOnce()
    {
        var userId = CreateUserId();
        var sessionId = Id<UserSession>.New();
        var beforeUnregister = DateTimeOffset.UtcNow;

        var result = await _service.UnregisterAsync(userId, sessionId, new PushInstallationActionInput(" device-1 "));
        var afterUnregister = DateTimeOffset.UtcNow;

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(_pushInstallationRepository.Calls
            .Where(call =>
                call.Method == nameof(IPushInstallationRepository.DisableBoundForUserAndSessionAsync)
                && call.Argument is ValueTuple<string, Id<User>, Id<UserSession>, DateTimeOffset, string> arguments
                && arguments.Item1 == "device-1"
                && arguments.Item2 == userId
                && arguments.Item3 == sessionId
                && arguments.Item4.Offset == TimeSpan.Zero
                && arguments.Item4 >= beforeUnregister
                && arguments.Item4 <= afterUnregister
                && arguments.Item5 == "Unregistered"), Has.Exactly(1).Items);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DisassociateAsync_StagesActionAndCommitsOnce()
    {
        var userId = CreateUserId();
        var sessionId = Id<UserSession>.New();
        var beforeDisassociate = DateTimeOffset.UtcNow;

        var result = await _service.DisassociateAsync(userId, sessionId, new PushInstallationActionInput(" device-1 "));
        var afterDisassociate = DateTimeOffset.UtcNow;

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(_pushInstallationRepository.Calls
            .Where(call =>
                call.Method == nameof(IPushInstallationRepository.DisassociateBoundForUserAndSessionAsync)
                && call.Argument is ValueTuple<string, Id<User>, Id<UserSession>, DateTimeOffset> arguments
                && arguments.Item1 == "device-1"
                && arguments.Item2 == userId
                && arguments.Item3 == sessionId
                && arguments.Item4.Offset == TimeSpan.Zero
                && arguments.Item4 >= beforeDisassociate
                && arguments.Item4 <= afterDisassociate), Has.Exactly(1).Items);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public async Task PushInstallationActions_WhenInstallationKeyIsBlank_DoNotWriteOrCommit(string? installationKey)
    {
        var userId = CreateUserId();
        var sessionId = Id<UserSession>.New();

        var unregisterResult = await _service.UnregisterAsync(userId, sessionId, new PushInstallationActionInput(installationKey!));
        var disassociateResult = await _service.DisassociateAsync(userId, sessionId, new PushInstallationActionInput(installationKey!));

        Assert.That(unregisterResult.Error, Is.TypeOf<InvalidUserError>());
        Assert.That(unregisterResult.Error.Message, Is.EqualTo(Messages.FieldRequired));
        Assert.That(disassociateResult.Error, Is.TypeOf<InvalidUserError>());
        Assert.That(disassociateResult.Error.Message, Is.EqualTo(Messages.FieldRequired));
        Assert.That((_pushInstallationRepository.Calls).Any(call => call.Method == nameof(IPushInstallationRepository.DisableBoundForUserAndSessionAsync)), Is.False);
        Assert.That((_pushInstallationRepository.Calls).Any(call => call.Method == nameof(IPushInstallationRepository.DisassociateBoundForUserAndSessionAsync)), Is.False);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PushInstallationActions_WhenSessionIsMissing_DoNotWriteOrCommit()
    {
        var userId = CreateUserId();

        var unregisterResult = await _service.UnregisterAsync(userId, null, new PushInstallationActionInput("device-1"));
        var disassociateResult = await _service.DisassociateAsync(userId, null, new PushInstallationActionInput("device-1"));

        Assert.That(unregisterResult.Error, Is.TypeOf<UserUnauthorizedError>());
        Assert.That(unregisterResult.Error.Message, Is.EqualTo(Messages.Unauthorized));
        Assert.That(disassociateResult.Error, Is.TypeOf<UserUnauthorizedError>());
        Assert.That(disassociateResult.Error.Message, Is.EqualTo(Messages.Unauthorized));
        Assert.That((_pushInstallationRepository.Calls).Any(call => call.Method == nameof(IPushInstallationRepository.DisableBoundForUserAndSessionAsync)), Is.False);
        Assert.That((_pushInstallationRepository.Calls).Any(call => call.Method == nameof(IPushInstallationRepository.DisassociateBoundForUserAndSessionAsync)), Is.False);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PushInstallationActions_WhenUserIsMissing_DoNotWriteOrCommit()
    {
        var sessionId = Id<UserSession>.New();

        var unregisterResult = await _service.UnregisterAsync(null, sessionId, new PushInstallationActionInput("device-1"));
        var disassociateResult = await _service.DisassociateAsync(null, sessionId, new PushInstallationActionInput("device-1"));

        Assert.That(unregisterResult.Error, Is.TypeOf<UserUnauthorizedError>());
        Assert.That(unregisterResult.Error.Message, Is.EqualTo(Messages.Unauthorized));
        Assert.That(disassociateResult.Error, Is.TypeOf<UserUnauthorizedError>());
        Assert.That(disassociateResult.Error.Message, Is.EqualTo(Messages.Unauthorized));
        Assert.That((_pushInstallationRepository.Calls).Any(call => call.Method == nameof(IPushInstallationRepository.DisableBoundForUserAndSessionAsync)), Is.False);
        Assert.That((_pushInstallationRepository.Calls).Any(call => call.Method == nameof(IPushInstallationRepository.DisassociateBoundForUserAndSessionAsync)), Is.False);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PushInstallationActions_WhenNoExactSessionBindingExists_ReturnForbiddenWithoutCommit()
    {
        var userId = CreateUserId();
        var sessionId = Id<UserSession>.New();
        _pushInstallationRepository.DisableBoundForUserAndSession = (_, _, _, _, _, _) => Task.FromResult<PushInstallation?>(null);
        _pushInstallationRepository.DisassociateBoundForUserAndSession = (_, _, _, _, _) => Task.FromResult<PushInstallation?>(null);

        var unregisterResult = await _service.UnregisterAsync(userId, sessionId, new PushInstallationActionInput("device-1"));
        var disassociateResult = await _service.DisassociateAsync(userId, sessionId, new PushInstallationActionInput("device-1"));

        Assert.That(unregisterResult.Error, Is.TypeOf<ForbiddenError>());
        Assert.That(disassociateResult.Error, Is.TypeOf<ForbiddenError>());
        Assert.That(_pushInstallationRepository.Calls
            .Where(call =>
                call.Method == nameof(IPushInstallationRepository.DisableBoundForUserAndSessionAsync)
                && call.Argument is ValueTuple<string, Id<User>, Id<UserSession>, DateTimeOffset, string> disableArguments
                && disableArguments.Item1 == "device-1"
                && disableArguments.Item2 == userId
                && disableArguments.Item3 == sessionId
                && disableArguments.Item5 == "Unregistered"), Has.Exactly(1).Items);
        Assert.That(_pushInstallationRepository.Calls
            .Where(call =>
                call.Method == nameof(IPushInstallationRepository.DisassociateBoundForUserAndSessionAsync)
                && call.Argument is ValueTuple<string, Id<User>, Id<UserSession>, DateTimeOffset> disassociateArguments
                && disassociateArguments.Item1 == "device-1"
                && disassociateArguments.Item2 == userId
                && disassociateArguments.Item3 == sessionId), Has.Exactly(1).Items);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task StageDisassociateForSessionAsync_StagesWithoutCommitting()
    {
        var sessionId = Id<AccountSessionReference>.New();
        var beforeDisassociate = DateTimeOffset.UtcNow;
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;

        await _service.StageDisassociateForSessionAsync(sessionId, cancellationToken);
        var afterDisassociate = DateTimeOffset.UtcNow;

        Assert.That(_pushInstallationRepository.Calls
            .Where(call =>
                call.Method == nameof(IPushInstallationRepository.DisassociateForSessionAsync)
                && call.Argument is ValueTuple<Id<AccountSessionReference>, DateTimeOffset> arguments
                && arguments.Item1 == sessionId
                && arguments.Item2.Offset == TimeSpan.Zero
                && arguments.Item2 >= beforeDisassociate
                && arguments.Item2 <= afterDisassociate
                && call.CancellationToken == cancellationToken), Has.Exactly(1).Items);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task StageRemoveForAccountAsync_StagesAllInstallationRemovalWithoutCommitting()
    {
        var accountId = Id<AccountReference>.New();
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;

        await _service.StageRemoveForAccountAsync(accountId, cancellationToken);

        Assert.That(_pushInstallationRepository.Calls
            .Where(call =>
                call.Method == nameof(IPushInstallationRepository.RemoveForAccountAsync)
                && call.Argument is Id<User> userId
                && userId == accountId.Rebind<User>()
                && call.CancellationToken == cancellationToken), Has.Exactly(1).Items);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static Id<User> CreateUserId()
    {
        return Id<User>.New();
    }

    private static DbUpdateException CreateUniqueViolation(string constraintName)
    {
        var postgresException = new PostgresException(
            "duplicate key value violates unique constraint",
            "ERROR",
            "ERROR",
            PostgresErrorCodes.UniqueViolation,
            constraintName: constraintName);
        return new DbUpdateException("duplicate push installation", postgresException);
    }

    private static AppDbContext CreateDatabase()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"push-registration-conflict-{Id<PushInstallationLifecycleServiceTests>.New():N}")
            .Options);

    private static PushInstallation CreateInstallation(string installationId, string token)
        => new()
        {
            Id = Id<PushInstallation>.New(),
            InstallationId = installationId,
            Platform = "android",
            FcmToken = token,
            Environment = "production",
            LastSeenAt = DateTimeOffset.UtcNow
        };

    private sealed class FailOnceUnitOfWork(AppDbContext database, Exception firstFailure) : IUnitOfWork
    {
        private bool _failed;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!_failed)
            {
                _failed = true;
                return Task.FromException<int>(firstFailure);
            }

            return database.SaveChangesAsync(cancellationToken);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.FromException<IUnitOfWorkTransaction>(new NotSupportedException());
    }

    private sealed class ConfigurableConflictClassifier : IPushInstallationPersistenceConflictClassifier
    {
        public bool ShouldClassify { get; set; }

        public bool IsRegistrationConflict(DbUpdateException exception) => ShouldClassify;
    }
}
