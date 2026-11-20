using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Pagination;
using LgymApi.Application.Platform.ReferenceData.AppConfig;
using LgymApi.Application.Platform.ReferenceData.AppConfig.Contracts;
using LgymApi.Application.Platform.ReferenceData.Errors;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Platform.Contracts;
using AppConfigEntity = LgymApi.Domain.Entities.AppConfig;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class AppConfigServiceTests
{
    [Test]
    public async Task GetLatestByPlatformAsync_DoesNotAuthorizeAndReturnsLatestConfig()
    {
        var port = new RecordingAppConfigAuthorizationPort(canManage: false);
        var config = new AppConfigEntity { Id = Id<AppConfigEntity>.New(), Platform = Platforms.Android };
        var repository = new RecordingAppConfigRepository { LatestConfig = config };
        var service = CreateService(port, repository, new RecordingUnitOfWork());

        var result = await service.GetLatestByPlatformAsync(Platforms.Android);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.SameAs(config));
        Assert.That(port.Calls, Is.Empty);
        Assert.That(repository.LatestLookupCount, Is.EqualTo(1));
    }

    [Test]
    public async Task GetLatestByPlatformAsync_UnknownPlatformReturnsInvalidWithoutRepositoryOrPortCall()
    {
        var port = new RecordingAppConfigAuthorizationPort(canManage: true);
        var repository = new RecordingAppConfigRepository();
        var service = CreateService(port, repository, new RecordingUnitOfWork());

        var result = await service.GetLatestByPlatformAsync(Platforms.Unknown);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<InvalidAppConfigError>());
        Assert.That(port.Calls, Is.Empty);
        Assert.That(repository.LatestLookupCount, Is.EqualTo(0));
    }

    [TestCaseSource(nameof(ForbiddenUserIds))]
    public async Task CreateNewAppVersionAsync_EmptyMissingOrDeniedUserReturnsForbiddenWithoutSave(Id<User> userId)
    {
        var port = new RecordingAppConfigAuthorizationPort(canManage: false);
        var repository = new RecordingAppConfigRepository();
        var unitOfWork = new RecordingUnitOfWork();
        var service = CreateService(port, repository, unitOfWork);

        var result = await service.CreateNewAppVersionAsync(userId, ValidCreateInput());

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<AppConfigForbiddenError>());
        var assertedValue82 = (port.Calls).ToArray();
        Assert.That(assertedValue82, Has.Exactly(1).Items);
        Assert.That(assertedValue82.Single(), Is.EqualTo(userId.Rebind<ActorReference>()));
        Assert.That(repository.Added, Is.Empty);
        Assert.That(unitOfWork.SaveChangesCount, Is.EqualTo(0));
    }

    [TestCase(ProtectedOperation.Create)]
    [TestCase(ProtectedOperation.List)]
    [TestCase(ProtectedOperation.Get)]
    [TestCase(ProtectedOperation.Update)]
    [TestCase(ProtectedOperation.Delete)]
    public async Task ProtectedOperation_AuthorizedUserCallsPortExactlyOnce(ProtectedOperation operation)
    {
        var userId = Id<User>.New();
        var config = new AppConfigEntity { Id = Id<AppConfigEntity>.New(), Platform = Platforms.Android };
        var port = new RecordingAppConfigAuthorizationPort(canManage: true);
        var repository = new RecordingAppConfigRepository { Config = config };
        var unitOfWork = new RecordingUnitOfWork();
        var service = CreateService(port, repository, unitOfWork);

        await InvokeAsync(operation, service, userId, config.Id);

        var assertedValue83 = (port.Calls).ToArray();
        Assert.That(assertedValue83, Has.Exactly(1).Items);
        Assert.That(assertedValue83.Single(), Is.EqualTo(userId.Rebind<ActorReference>()));
    }

    [TestCase(ProtectedOperation.Create)]
    [TestCase(ProtectedOperation.List)]
    [TestCase(ProtectedOperation.Get)]
    [TestCase(ProtectedOperation.Update)]
    [TestCase(ProtectedOperation.Delete)]
    public async Task ProtectedOperation_DeniedUserReturnsForbiddenWithoutRepositoryMutationOrSave(ProtectedOperation operation)
    {
        var port = new RecordingAppConfigAuthorizationPort(canManage: false);
        var repository = new RecordingAppConfigRepository();
        var unitOfWork = new RecordingUnitOfWork();
        var service = CreateService(port, repository, unitOfWork);

        await AssertForbiddenAsync(operation, service, Id<User>.New(), Id<AppConfigEntity>.New());

        Assert.That(port.Calls, Has.Exactly(1).Items);
        Assert.That(repository.TotalCalls, Is.EqualTo(0));
        Assert.That(repository.Added, Is.Empty);
        Assert.That(repository.DeleteCount, Is.EqualTo(0));
        Assert.That(unitOfWork.SaveChangesCount, Is.EqualTo(0));
    }

    [Test]
    public async Task CreateNewAppVersionAsync_InvalidPlatformAfterAuthorizationReturnsInvalidWithoutSave()
    {
        var port = new RecordingAppConfigAuthorizationPort(canManage: true);
        var repository = new RecordingAppConfigRepository();
        var unitOfWork = new RecordingUnitOfWork();
        var service = CreateService(port, repository, unitOfWork);

        var result = await service.CreateNewAppVersionAsync(Id<User>.New(), ValidCreateInput(Platforms.Unknown));

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<InvalidAppConfigError>());
        Assert.That(port.Calls, Has.Exactly(1).Items);
        Assert.That(repository.Added, Is.Empty);
        Assert.That(unitOfWork.SaveChangesCount, Is.EqualTo(0));
    }

    [TestCase(ProtectedOperation.Get)]
    [TestCase(ProtectedOperation.Update)]
    [TestCase(ProtectedOperation.Delete)]
    public async Task ProtectedOperation_EmptyConfigIdAfterAuthorizationReturnsInvalidWithoutSave(ProtectedOperation operation)
    {
        var port = new RecordingAppConfigAuthorizationPort(canManage: true);
        var repository = new RecordingAppConfigRepository();
        var unitOfWork = new RecordingUnitOfWork();
        var service = CreateService(port, repository, unitOfWork);

        await AssertInvalidConfigIdAsync(operation, service, Id<User>.New());

        Assert.That(port.Calls, Has.Exactly(1).Items);
        Assert.That(repository.TotalCalls, Is.EqualTo(0));
        Assert.That(unitOfWork.SaveChangesCount, Is.EqualTo(0));
    }

    [Test]
    public async Task UpdateAsync_UnknownPlatformAfterAuthorizationReturnsInvalidWithoutRepositoryOrSave()
    {
        var port = new RecordingAppConfigAuthorizationPort(canManage: true);
        var repository = new RecordingAppConfigRepository();
        var unitOfWork = new RecordingUnitOfWork();
        var service = CreateService(port, repository, unitOfWork);

        var result = await service.UpdateAsync(Id<User>.New(), Id<AppConfigEntity>.New(), ValidUpdateInput(Platforms.Unknown));

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<InvalidAppConfigError>());
        Assert.That(port.Calls, Has.Exactly(1).Items);
        Assert.That(repository.TotalCalls, Is.EqualTo(0));
        Assert.That(unitOfWork.SaveChangesCount, Is.EqualTo(0));
    }

    [TestCase(ProtectedOperation.Get)]
    [TestCase(ProtectedOperation.Update)]
    [TestCase(ProtectedOperation.Delete)]
    public async Task ProtectedOperation_MissingConfigReturnsNotFoundWithoutSave(ProtectedOperation operation)
    {
        var port = new RecordingAppConfigAuthorizationPort(canManage: true);
        var repository = new RecordingAppConfigRepository();
        var unitOfWork = new RecordingUnitOfWork();
        var service = CreateService(port, repository, unitOfWork);

        await AssertNotFoundAsync(operation, service, Id<User>.New(), Id<AppConfigEntity>.New());

        Assert.That(port.Calls, Has.Exactly(1).Items);
        Assert.That(unitOfWork.SaveChangesCount, Is.EqualTo(0));
    }

    [Test]
    public async Task CreateNewAppVersionAsync_AuthorizedUserStagesConfigAndCommitsOnce()
    {
        var port = new RecordingAppConfigAuthorizationPort(canManage: true);
        var repository = new RecordingAppConfigRepository();
        var unitOfWork = new RecordingUnitOfWork();
        var service = CreateService(port, repository, unitOfWork);

        var result = await service.CreateNewAppVersionAsync(Id<User>.New(), ValidCreateInput());

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(repository.Added, Has.Exactly(1).Items);
        Assert.That(unitOfWork.SaveChangesCount, Is.EqualTo(1));
    }

    [Test]
    public async Task UpdateAndDelete_AuthorizedUserCommitOnceAfterTrackedConfigMutation()
    {
        var userId = Id<User>.New();
        var config = new AppConfigEntity { Id = Id<AppConfigEntity>.New(), Platform = Platforms.Android };
        var port = new RecordingAppConfigAuthorizationPort(canManage: true);
        var repository = new RecordingAppConfigRepository { Config = config };
        var unitOfWork = new RecordingUnitOfWork();
        var service = CreateService(port, repository, unitOfWork);

        var update = await service.UpdateAsync(userId, config.Id, ValidUpdateInput());
        var delete = await service.DeleteAsync(userId, config.Id);

        Assert.That(update.IsSuccess, Is.True);
        Assert.That(delete.IsSuccess, Is.True);
        Assert.That(repository.DeleteCount, Is.EqualTo(1));
        Assert.That(unitOfWork.SaveChangesCount, Is.EqualTo(2));
        Assert.That(port.Calls, Has.Exactly(2).Items);
    }

    [Test]
    public async Task AuthorizationPortFailure_PropagatesWithoutRepositoryOrSave()
    {
        var expected = new InvalidOperationException("Authorization port failed.");
        var port = new RecordingAppConfigAuthorizationPort(canManage: false, exception: expected);
        var repository = new RecordingAppConfigRepository();
        var unitOfWork = new RecordingUnitOfWork();
        var service = CreateService(port, repository, unitOfWork);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.CreateNewAppVersionAsync(Id<User>.New(), ValidCreateInput()));

        Assert.That(exception, Is.SameAs(expected));
        Assert.That(repository.TotalCalls, Is.EqualTo(0));
        Assert.That(unitOfWork.SaveChangesCount, Is.EqualTo(0));
    }

    private static IEnumerable<TestCaseData> ForbiddenUserIds()
    {
        yield return new TestCaseData(Id<User>.Empty).SetName("CreateNewAppVersionAsync_EmptyUserId_ReturnsForbidden");
        yield return new TestCaseData(Id<User>.New()).SetName("CreateNewAppVersionAsync_MissingUser_ReturnsForbidden");
        yield return new TestCaseData(Id<User>.New()).SetName("CreateNewAppVersionAsync_DeniedUser_ReturnsForbidden");
    }

    private static AppConfigService CreateService(
        IAppConfigAuthorizationPort authorizationPort,
        IAppConfigRepository repository,
        IUnitOfWork unitOfWork) => new(authorizationPort, repository, unitOfWork);

    private static CreateAppVersionInput ValidCreateInput(Platforms platform = Platforms.Android) =>
        new(platform, "1.0", "1.1", false, "https://example.test", "notes");

    private static UpdateAppConfigInput ValidUpdateInput(Platforms platform = Platforms.Android) =>
        new(platform, "1.0", "1.1", false, "https://example.test", "notes");

    private static async Task InvokeAsync(ProtectedOperation operation, AppConfigService service, Id<User> userId, Id<AppConfigEntity> configId)
    {
        switch (operation)
        {
            case ProtectedOperation.Create:
                Assert.That((await service.CreateNewAppVersionAsync(userId, ValidCreateInput())).IsSuccess, Is.True);
                break;
            case ProtectedOperation.List:
                Assert.That((await service.GetPaginatedAsync(userId, new FilterInput())).IsSuccess, Is.True);
                break;
            case ProtectedOperation.Get:
                Assert.That((await service.GetByIdAsync(userId, configId)).IsSuccess, Is.True);
                break;
            case ProtectedOperation.Update:
                Assert.That((await service.UpdateAsync(userId, configId, ValidUpdateInput())).IsSuccess, Is.True);
                break;
            case ProtectedOperation.Delete:
                Assert.That((await service.DeleteAsync(userId, configId)).IsSuccess, Is.True);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
        }
    }

    private static async Task AssertForbiddenAsync(ProtectedOperation operation, AppConfigService service, Id<User> userId, Id<AppConfigEntity> configId)
    {
        switch (operation)
        {
            case ProtectedOperation.Create:
                Assert.That((await service.CreateNewAppVersionAsync(userId, ValidCreateInput())).Error, Is.TypeOf<AppConfigForbiddenError>());
                break;
            case ProtectedOperation.List:
                Assert.That((await service.GetPaginatedAsync(userId, new FilterInput())).Error, Is.TypeOf<AppConfigForbiddenError>());
                break;
            case ProtectedOperation.Get:
                Assert.That((await service.GetByIdAsync(userId, configId)).Error, Is.TypeOf<AppConfigForbiddenError>());
                break;
            case ProtectedOperation.Update:
                Assert.That((await service.UpdateAsync(userId, configId, ValidUpdateInput())).Error, Is.TypeOf<AppConfigForbiddenError>());
                break;
            case ProtectedOperation.Delete:
                Assert.That((await service.DeleteAsync(userId, configId)).Error, Is.TypeOf<AppConfigForbiddenError>());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
        }
    }

    private static async Task AssertInvalidConfigIdAsync(ProtectedOperation operation, AppConfigService service, Id<User> userId)
    {
        switch (operation)
        {
            case ProtectedOperation.Get:
                Assert.That((await service.GetByIdAsync(userId, Id<AppConfigEntity>.Empty)).Error, Is.TypeOf<InvalidAppConfigError>());
                break;
            case ProtectedOperation.Update:
                Assert.That((await service.UpdateAsync(userId, Id<AppConfigEntity>.Empty, ValidUpdateInput())).Error, Is.TypeOf<InvalidAppConfigError>());
                break;
            case ProtectedOperation.Delete:
                Assert.That((await service.DeleteAsync(userId, Id<AppConfigEntity>.Empty)).Error, Is.TypeOf<InvalidAppConfigError>());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
        }
    }

    private static async Task AssertNotFoundAsync(ProtectedOperation operation, AppConfigService service, Id<User> userId, Id<AppConfigEntity> configId)
    {
        switch (operation)
        {
            case ProtectedOperation.Get:
                Assert.That((await service.GetByIdAsync(userId, configId)).Error, Is.TypeOf<AppConfigNotFoundError>());
                break;
            case ProtectedOperation.Update:
                Assert.That((await service.UpdateAsync(userId, configId, ValidUpdateInput())).Error, Is.TypeOf<AppConfigNotFoundError>());
                break;
            case ProtectedOperation.Delete:
                Assert.That((await service.DeleteAsync(userId, configId)).Error, Is.TypeOf<AppConfigNotFoundError>());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
        }
    }

    public enum ProtectedOperation
    {
        Create,
        List,
        Get,
        Update,
        Delete
    }

    private sealed class RecordingAppConfigAuthorizationPort(bool canManage, Exception? exception = null) : IAppConfigAuthorizationPort
    {
        public List<Id<ActorReference>> Calls { get; } = [];

        public Task<bool> CanManageAppConfigAsync(Id<ActorReference> actorId, CancellationToken cancellationToken = default)
        {
            Calls.Add(actorId);
            return exception is null ? Task.FromResult(canManage) : Task.FromException<bool>(exception);
        }
    }

    private sealed class RecordingAppConfigRepository : IAppConfigRepository
    {
        public AppConfigEntity? LatestConfig { get; init; }
        public AppConfigEntity? Config { get; init; }
        public List<AppConfigEntity> Added { get; } = [];
        public int LatestLookupCount { get; private set; }
        public int FindCount { get; private set; }
        public int FindTrackedCount { get; private set; }
        public int PaginationCount { get; private set; }
        public int DeleteCount { get; private set; }
        public int TotalCalls => LatestLookupCount + FindCount + FindTrackedCount + PaginationCount + DeleteCount + Added.Count;

        public Task<AppConfigEntity?> GetLatestByPlatformAsync(Platforms platform, CancellationToken cancellationToken = default)
        {
            LatestLookupCount++;
            return Task.FromResult(LatestConfig);
        }

        public Task AddAsync(AppConfigEntity config, CancellationToken cancellationToken = default)
        {
            Added.Add(config);
            return Task.CompletedTask;
        }

        public Task<AppConfigEntity?> FindByIdAsync(Id<AppConfigEntity> id, CancellationToken cancellationToken = default)
        {
            FindCount++;
            return Task.FromResult(Config?.Id == id ? Config : null);
        }

        public Task<AppConfigEntity?> FindByIdTrackedAsync(Id<AppConfigEntity> id, CancellationToken cancellationToken = default)
        {
            FindTrackedCount++;
            return Task.FromResult(Config?.Id == id ? Config : null);
        }

        public Task<Pagination<AppConfigEntity>> GetPaginatedAsync(FilterInput filterInput, CancellationToken cancellationToken = default)
        {
            PaginationCount++;
            return Task.FromResult(new Pagination<AppConfigEntity>());
        }

        public void Update(AppConfigEntity config) { }

        public void Delete(AppConfigEntity config)
        {
            DeleteCount++;
        }
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.FromResult(1);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void DetachEntity<TEntity>(TEntity entity) where TEntity : class => throw new NotSupportedException();
    }
}
