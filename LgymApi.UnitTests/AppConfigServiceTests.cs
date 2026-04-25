using LgymApi.Application.Common.Errors;
using FluentAssertions;
using LgymApi.Application.Features.AdminManagement.Models;
using LgymApi.Application.AppConfig;
using LgymApi.Application.Models;
using LgymApi.Application.Pagination;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class AppConfigServiceTests
{
    [Test]
    public async Task GetLatestByPlatformAsync_WithUnknownPlatform_ReturnsInvalidAppConfigError()
    {
        var service = new AppConfigService(
            new NoOpUserRepository(),
            new NoOpRoleRepository(),
            new NoOpAppConfigRepository(),
            new NoOpUnitOfWork());

        var result = await service.GetLatestByPlatformAsync(Platforms.Unknown);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidAppConfigError>();
    }

    // GetPaginatedAsync Tests
    [Test]
    public async Task GetPaginatedAsync_WithEmptyUserId_ReturnsForbidden()
    {
        var service = new AppConfigService(
            new NoOpUserRepository(),
            new NoOpRoleRepository(),
            new NoOpAppConfigRepository(),
            new NoOpUnitOfWork());

        var result = await service.GetPaginatedAsync(Id<User>.Empty, new FilterInput());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<AppConfigForbiddenError>();
    }

    [Test]
    public async Task GetPaginatedAsync_WithNoPermission_ReturnsForbidden()
    {
        var userId = Id<User>.New();
        var userRepo = new FakeUserRepository(userId);
        var roleRepo = new NoOpRoleRepository(); // UserHasPermissionAsync returns false

        var service = new AppConfigService(
            userRepo,
            roleRepo,
            new NoOpAppConfigRepository(),
            new NoOpUnitOfWork());

        var result = await service.GetPaginatedAsync(userId, new FilterInput());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<AppConfigForbiddenError>();
    }

    [Test]
    public async Task GetPaginatedAsync_WithPermission_ReturnsSuccess()
    {
        var userId = Id<User>.New();
        var userRepo = new FakeUserRepository(userId);
        var roleRepo = new FakeRoleRepository(hasPermission: true);
        var appConfigRepo = new FakeAppConfigRepository();

        var service = new AppConfigService(
            userRepo,
            roleRepo,
            appConfigRepo,
            new NoOpUnitOfWork());

        var result = await service.GetPaginatedAsync(userId, new FilterInput());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    // GetByIdAsync Tests
    [Test]
    public async Task GetByIdAsync_WithEmptyUserId_ReturnsForbidden()
    {
        var service = new AppConfigService(
            new NoOpUserRepository(),
            new NoOpRoleRepository(),
            new NoOpAppConfigRepository(),
            new NoOpUnitOfWork());

        var result = await service.GetByIdAsync(Id<User>.Empty, Id<AppConfig>.New());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<AppConfigForbiddenError>();
    }

    [Test]
    public async Task GetByIdAsync_WithEmptyConfigId_ReturnsInvalid()
    {
        var userId = Id<User>.New();
        var userRepo = new FakeUserRepository(userId);
        var roleRepo = new FakeRoleRepository(hasPermission: true);

        var service = new AppConfigService(
            userRepo,
            roleRepo,
            new NoOpAppConfigRepository(),
            new NoOpUnitOfWork());

        var result = await service.GetByIdAsync(userId, Id<AppConfig>.Empty);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidAppConfigError>();
    }

    [Test]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsNotFound()
    {
        var userId = Id<User>.New();
        var userRepo = new FakeUserRepository(userId);
        var roleRepo = new FakeRoleRepository(hasPermission: true);
        var appConfigRepo = new FakeAppConfigRepository();

        var service = new AppConfigService(
            userRepo,
            roleRepo,
            appConfigRepo,
            new NoOpUnitOfWork());

        var result = await service.GetByIdAsync(userId, Id<AppConfig>.New());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<AppConfigNotFoundError>();
    }

     [Test]
     public async Task GetByIdAsync_WithValidId_ReturnsSuccess()
     {
         var userId = Id<User>.New();
         var configId = Id<AppConfig>.New();
         var userRepo = new FakeUserRepository(userId);
         var roleRepo = new FakeRoleRepository(hasPermission: true);
         var appConfigRepo = new FakeAppConfigRepository(configId);

         var service = new AppConfigService(
             userRepo,
             roleRepo,
             appConfigRepo,
             new NoOpUnitOfWork());

         var result = await service.GetByIdAsync(userId, configId);

         result.IsSuccess.Should().BeTrue();
         result.Value.Should().NotBeNull();
         result.Value.Id.Should().Be(configId);
     }

     [Test]
     public async Task GetByIdAsync_WithNoPermission_ReturnsForbiddenError()
     {
         var userId = Id<User>.New();
         var configId = Id<AppConfig>.New();
         var userRepo = new FakeUserRepository(userId);
         var roleRepo = new FakeRoleRepository(hasPermission: false);
         var appConfigRepo = new FakeAppConfigRepository(configId);

         var service = new AppConfigService(
             userRepo,
             roleRepo,
             appConfigRepo,
             new NoOpUnitOfWork());

         var result = await service.GetByIdAsync(userId, configId);

         result.IsFailure.Should().BeTrue();
         result.Error.Should().BeOfType<AppConfigForbiddenError>();
     }

     // UpdateAsync Tests
    [Test]
    public async Task UpdateAsync_WithEmptyUserId_ReturnsForbidden()
    {
        var service = new AppConfigService(
            new NoOpUserRepository(),
            new NoOpRoleRepository(),
            new NoOpAppConfigRepository(),
            new NoOpUnitOfWork());

        var input = new UpdateAppConfigInput(Platforms.Android, "1.0", "1.1", false, null, null);
        var result = await service.UpdateAsync(Id<User>.Empty, Id<AppConfig>.New(), input);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<AppConfigForbiddenError>();
    }

    [Test]
    public async Task UpdateAsync_WithNonExistentId_ReturnsNotFound()
    {
        var userId = Id<User>.New();
        var userRepo = new FakeUserRepository(userId);
        var roleRepo = new FakeRoleRepository(hasPermission: true);
        var appConfigRepo = new FakeAppConfigRepository();

        var service = new AppConfigService(
            userRepo,
            roleRepo,
            appConfigRepo,
            new NoOpUnitOfWork());

        var input = new UpdateAppConfigInput(Platforms.Android, "1.0", "1.1", false, null, null);
        var result = await service.UpdateAsync(userId, Id<AppConfig>.New(), input);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<AppConfigNotFoundError>();
    }

    [Test]
    public async Task UpdateAsync_WithUnknownPlatform_ReturnsInvalid()
    {
        var userId = Id<User>.New();
        var configId = Id<AppConfig>.New();
        var userRepo = new FakeUserRepository(userId);
        var roleRepo = new FakeRoleRepository(hasPermission: true);
        var appConfigRepo = new FakeAppConfigRepository(configId, tracked: true);

        var service = new AppConfigService(
            userRepo,
            roleRepo,
            appConfigRepo,
            new NoOpUnitOfWork());

        var input = new UpdateAppConfigInput(Platforms.Unknown, "1.0", "1.1", false, null, null);
        var result = await service.UpdateAsync(userId, configId, input);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidAppConfigError>();
    }

     [Test]
     public async Task UpdateAsync_WithValidInput_ReturnsSuccess()
     {
         var userId = Id<User>.New();
         var configId = Id<AppConfig>.New();
         var userRepo = new FakeUserRepository(userId);
         var roleRepo = new FakeRoleRepository(hasPermission: true);
         var appConfigRepo = new FakeAppConfigRepository(configId, tracked: true);
         var uow = new FakeUnitOfWork();

         var service = new AppConfigService(
             userRepo,
             roleRepo,
             appConfigRepo,
             uow);

         var input = new UpdateAppConfigInput(Platforms.Android, "1.0", "1.1", false, "url", "notes");
         var result = await service.UpdateAsync(userId, configId, input);

         result.IsSuccess.Should().BeTrue();
         uow.SaveChangesCalled.Should().BeTrue();
     }

     [Test]
     public async Task UpdateAsync_WithNoPermission_ReturnsForbiddenError()
     {
         var userId = Id<User>.New();
         var configId = Id<AppConfig>.New();
         var userRepo = new FakeUserRepository(userId);
         var roleRepo = new FakeRoleRepository(hasPermission: false);
         var appConfigRepo = new FakeAppConfigRepository(configId, tracked: true);

         var service = new AppConfigService(
             userRepo,
             roleRepo,
             appConfigRepo,
             new NoOpUnitOfWork());

         var input = new UpdateAppConfigInput(Platforms.Android, "1.0", "1.1", false, null, null);
         var result = await service.UpdateAsync(userId, configId, input);

         result.IsFailure.Should().BeTrue();
         result.Error.Should().BeOfType<AppConfigForbiddenError>();
     }

     // DeleteAsync Tests
    [Test]
    public async Task DeleteAsync_WithEmptyUserId_ReturnsForbidden()
    {
        var service = new AppConfigService(
            new NoOpUserRepository(),
            new NoOpRoleRepository(),
            new NoOpAppConfigRepository(),
            new NoOpUnitOfWork());

        var result = await service.DeleteAsync(Id<User>.Empty, Id<AppConfig>.New());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<AppConfigForbiddenError>();
    }

    [Test]
    public async Task DeleteAsync_WithNonExistentId_ReturnsNotFound()
    {
        var userId = Id<User>.New();
        var userRepo = new FakeUserRepository(userId);
        var roleRepo = new FakeRoleRepository(hasPermission: true);
        var appConfigRepo = new FakeAppConfigRepository();

        var service = new AppConfigService(
            userRepo,
            roleRepo,
            appConfigRepo,
            new NoOpUnitOfWork());

        var result = await service.DeleteAsync(userId, Id<AppConfig>.New());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<AppConfigNotFoundError>();
    }

     [Test]
     public async Task DeleteAsync_WithValidId_ReturnsSuccess()
     {
         var userId = Id<User>.New();
         var configId = Id<AppConfig>.New();
         var userRepo = new FakeUserRepository(userId);
         var roleRepo = new FakeRoleRepository(hasPermission: true);
         var appConfigRepo = new FakeAppConfigRepository(configId, tracked: true);
         var uow = new FakeUnitOfWork();

         var service = new AppConfigService(
             userRepo,
             roleRepo,
             appConfigRepo,
             uow);

         var result = await service.DeleteAsync(userId, configId);

         result.IsSuccess.Should().BeTrue();
         appConfigRepo.DeleteCalled.Should().BeTrue();
         uow.SaveChangesCalled.Should().BeTrue();
     }

     [Test]
     public async Task DeleteAsync_WithNoPermission_ReturnsForbiddenError()
     {
         var userId = Id<User>.New();
         var configId = Id<AppConfig>.New();
         var userRepo = new FakeUserRepository(userId);
         var roleRepo = new FakeRoleRepository(hasPermission: false);
         var appConfigRepo = new FakeAppConfigRepository(configId, tracked: true);

         var service = new AppConfigService(
             userRepo,
             roleRepo,
             appConfigRepo,
             new NoOpUnitOfWork());

         var result = await service.DeleteAsync(userId, configId);

         result.IsFailure.Should().BeTrue();
         result.Error.Should().BeOfType<AppConfigForbiddenError>();
     }

     private sealed class NoOpUserRepository : IUserRepository
    {
        public Task<User?> FindByIdAsync(Id<User> id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByIdIncludingDeletedAsync(Id<User> id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByIdWithRolesAsync(Id<User> id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByNameAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByNameOrEmailAsync(string name, string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<LgymApi.Application.Models.UserRankingEntry>> GetRankingAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(User user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Pagination<UserResult>> GetUsersPaginatedAsync(FilterInput filterInput, bool includeDeleted, CancellationToken cancellationToken = default)
            => Task.FromResult(new Pagination<UserResult>());
    }

    private sealed class NoOpRoleRepository : IRoleRepository
    {
        public Task<List<Role>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Role?> FindByIdAsync(Id<Role> roleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Role?> FindByNameAsync(string roleName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Role>> GetByNamesAsync(IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ExistsByNameAsync(string roleName, Id<Role>? excludeRoleId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<string>> GetRoleNamesByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<string>> GetPermissionClaimsByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<string>> GetPermissionClaimsByRoleIdAsync(Id<Role> roleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Dictionary<Id<Role>, List<string>>> GetPermissionClaimsByRoleIdsAsync(IReadOnlyCollection<Id<Role>> roleIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UserHasRoleAsync(Id<User> userId, string roleName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UserHasPermissionAsync(Id<User> userId, string permission, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddRoleAsync(Role role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateRoleAsync(Role role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteRoleAsync(Role role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ReplaceRolePermissionClaimsAsync(Id<Role> roleId, IReadOnlyCollection<string> permissionClaims, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddUserRolesAsync(Id<User> userId, IReadOnlyCollection<Id<Role>> roleIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ReplaceUserRolesAsync(Id<User> userId, IReadOnlyCollection<Id<Role>> roleIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Dictionary<Id<User>, List<string>>> GetRoleNamesByUserIdsAsync(IReadOnlyCollection<Id<User>> userIds, CancellationToken cancellationToken = default)
            => Task.FromResult(new Dictionary<Id<User>, List<string>>());
        public Task<Pagination<Role>> GetRolesPaginatedAsync(FilterInput filterInput, CancellationToken cancellationToken = default)
            => Task.FromResult(new Pagination<Role>());
    }

    private sealed class NoOpAppConfigRepository : IAppConfigRepository
    {
        public Task<AppConfig?> GetLatestByPlatformAsync(Platforms platform, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(AppConfig config, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AppConfig?> FindByIdAsync(Id<AppConfig> id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AppConfig?> FindByIdTrackedAsync(Id<AppConfig> id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Pagination<AppConfig>> GetPaginatedAsync(FilterInput filterInput, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Update(AppConfig config) => throw new NotSupportedException();
        public void Delete(AppConfig config) => throw new NotSupportedException();
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void DetachEntity<TEntity>(TEntity entity) where TEntity : class => throw new NotSupportedException();
    }

    // Test-specific fakes with controlled behavior
    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly Id<User> _validUserId;

        public FakeUserRepository(Id<User> validUserId)
        {
            _validUserId = validUserId;
        }

        public Task<User?> FindByIdAsync(Id<User> id, CancellationToken cancellationToken = default)
        {
            if (id == _validUserId)
            {
                return Task.FromResult<User?>(new User { Id = id });
            }
            return Task.FromResult<User?>(null);
        }

        public Task<User?> FindByIdIncludingDeletedAsync(Id<User> id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByIdWithRolesAsync(Id<User> id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByNameAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByNameOrEmailAsync(string name, string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<LgymApi.Application.Models.UserRankingEntry>> GetRankingAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(User user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Pagination<UserResult>> GetUsersPaginatedAsync(FilterInput filterInput, bool includeDeleted, CancellationToken cancellationToken = default)
            => Task.FromResult(new Pagination<UserResult>());
    }

    private sealed class FakeRoleRepository : IRoleRepository
    {
        private readonly bool _hasPermission;

        public FakeRoleRepository(bool hasPermission)
        {
            _hasPermission = hasPermission;
        }

        public Task<bool> UserHasPermissionAsync(Id<User> userId, string permission, CancellationToken cancellationToken = default)
            => Task.FromResult(_hasPermission);

        public Task<List<Role>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Role?> FindByIdAsync(Id<Role> roleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Role?> FindByNameAsync(string roleName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Role>> GetByNamesAsync(IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ExistsByNameAsync(string roleName, Id<Role>? excludeRoleId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<string>> GetRoleNamesByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<string>> GetPermissionClaimsByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<string>> GetPermissionClaimsByRoleIdAsync(Id<Role> roleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Dictionary<Id<Role>, List<string>>> GetPermissionClaimsByRoleIdsAsync(IReadOnlyCollection<Id<Role>> roleIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UserHasRoleAsync(Id<User> userId, string roleName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddRoleAsync(Role role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateRoleAsync(Role role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteRoleAsync(Role role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ReplaceRolePermissionClaimsAsync(Id<Role> roleId, IReadOnlyCollection<string> permissionClaims, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddUserRolesAsync(Id<User> userId, IReadOnlyCollection<Id<Role>> roleIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ReplaceUserRolesAsync(Id<User> userId, IReadOnlyCollection<Id<Role>> roleIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Dictionary<Id<User>, List<string>>> GetRoleNamesByUserIdsAsync(IReadOnlyCollection<Id<User>> userIds, CancellationToken cancellationToken = default)
            => Task.FromResult(new Dictionary<Id<User>, List<string>>());
        public Task<Pagination<Role>> GetRolesPaginatedAsync(FilterInput filterInput, CancellationToken cancellationToken = default)
            => Task.FromResult(new Pagination<Role>());
    }

    private sealed class FakeAppConfigRepository : IAppConfigRepository
    {
        private readonly Id<AppConfig>? _validConfigId;
        private readonly bool _isTracked;
        public bool DeleteCalled { get; private set; }

        public FakeAppConfigRepository(Id<AppConfig>? validConfigId = null, bool tracked = false)
        {
            _validConfigId = validConfigId;
            _isTracked = tracked;
        }

        public Task<AppConfig?> FindByIdAsync(Id<AppConfig> id, CancellationToken cancellationToken = default)
        {
            if (!_isTracked && _validConfigId.HasValue && id == _validConfigId.Value)
            {
                return Task.FromResult<AppConfig?>(new AppConfig { Id = id, Platform = Platforms.Android });
            }
            return Task.FromResult<AppConfig?>(null);
        }

        public Task<AppConfig?> FindByIdTrackedAsync(Id<AppConfig> id, CancellationToken cancellationToken = default)
        {
            if (_isTracked && _validConfigId.HasValue && id == _validConfigId.Value)
            {
                return Task.FromResult<AppConfig?>(new AppConfig { Id = id, Platform = Platforms.Android });
            }
            return Task.FromResult<AppConfig?>(null);
        }

        public Task<Pagination<AppConfig>> GetPaginatedAsync(FilterInput filterInput, CancellationToken cancellationToken = default)
            => Task.FromResult(new Pagination<AppConfig>());

        public void Delete(AppConfig config)
        {
            DeleteCalled = true;
        }

        public Task<AppConfig?> GetLatestByPlatformAsync(Platforms platform, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(AppConfig config, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Update(AppConfig config) => throw new NotSupportedException();
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public bool SaveChangesCalled { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalled = true;
            return Task.FromResult(1);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void DetachEntity<TEntity>(TEntity entity) where TEntity : class => throw new NotSupportedException();
    }
}
