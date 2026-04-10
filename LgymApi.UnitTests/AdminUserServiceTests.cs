using System.Net;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Features.AdminManagement;
using LgymApi.Application.Features.AdminManagement.Models;
using LgymApi.Application.Models;
using LgymApi.Application.Pagination;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.UnitTests.Fakes;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class AdminUserServiceTests
{
    private AdminUserService _service = null!;
    private InMemoryAdminUserRepository _userRepository = null!;
    private InMemoryAdminRoleRepository _roleRepository = null!;
    private FakeUserSessionStore _sessionStore = null!;
    private FakeUnitOfWork _unitOfWork = null!;

    [SetUp]
    public void SetUp()
    {
        _userRepository = new InMemoryAdminUserRepository();
        _roleRepository = new InMemoryAdminRoleRepository();
        _sessionStore = new FakeUserSessionStore();
        _unitOfWork = new FakeUnitOfWork();
        _service = new AdminUserService(_userRepository, _roleRepository, _sessionStore, _unitOfWork);
    }

    [Test]
    public async Task GetUserAsync_ReturnsUserWithRoles_WhenUserExists()
    {
        var userId = Id<User>.New();
        var roleId = Id<Role>.New();
        _userRepository.Users.Add(new User { Id = (Domain.ValueObjects.Id<User>)userId, Name = "Test", Email = new Email("test@test.com") });
        _roleRepository.UserRoles[userId] = new List<Id<Role>> { roleId };
        _roleRepository.Roles.Add(new Role { Id = (Domain.ValueObjects.Id<Role>)roleId, Name = "Admin" });

        var result = await _service.GetUserAsync(userId);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Name, Is.EqualTo("Test"));
            Assert.That(result.Value.Roles, Has.Count.EqualTo(1));
            Assert.That(result.Value.Roles[0], Is.EqualTo("Admin"));
        });
    }

    [Test]
    public async Task GetUserAsync_ReturnsFailure_WhenUserNotFound()
    {
        var result = await _service.GetUserAsync(Id<User>.New());

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.InstanceOf<NotFoundError>());
        });
    }

    [Test]
    public async Task BlockUserAsync_BlocksUserAndRevokesAllSessions()
    {
        var userId = Id<User>.New();
        var adminId = Id<User>.New();
        _userRepository.Users.Add(new User { Id = (Domain.ValueObjects.Id<User>)userId, Name = "Test", Email = new Email("test@test.com") });
        await _sessionStore.CreateSessionAsync(userId, DateTimeOffset.UtcNow.AddDays(30), CancellationToken.None);

        var result = await _service.BlockUserAsync(userId, adminId);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(_userRepository.Users.First(u => u.Id == userId).IsBlocked, Is.True);
            Assert.That(_sessionStore.RevokedAllUserIds, Contains.Item(userId));
            Assert.That(_unitOfWork.SaveChangesCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task BlockUserAsync_ReturnsFailure_WhenBlockingSelf()
    {
        var userId = Id<User>.New();
        _userRepository.Users.Add(new User { Id = (Domain.ValueObjects.Id<User>)userId, Name = "Test", Email = new Email("test@test.com") });

        var result = await _service.BlockUserAsync(userId, userId);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.InstanceOf<ForbiddenError>());
        });
    }

    [Test]
    public async Task UnblockUserAsync_UnblocksUser()
    {
        var userId = Id<User>.New();
        _userRepository.Users.Add(new User { Id = (Domain.ValueObjects.Id<User>)userId, Name = "Test", Email = new Email("test@test.com"), IsBlocked = true });

        var result = await _service.UnblockUserAsync(userId);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(_userRepository.Users.First(u => u.Id == userId).IsBlocked, Is.False);
        });
    }

    [Test]
    public async Task DeleteUserAsync_SoftDeletesUserAndRevokesAllSessions()
    {
        var userId = Id<User>.New();
        var adminId = Id<User>.New();
        _userRepository.Users.Add(new User { Id = (Domain.ValueObjects.Id<User>)userId, Name = "Test", Email = new Email("test@test.com") });
        await _sessionStore.CreateSessionAsync(userId, DateTimeOffset.UtcNow.AddDays(30), CancellationToken.None);

        var result = await _service.DeleteUserAsync(userId, adminId);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(_userRepository.Users.First(u => u.Id == userId).IsDeleted, Is.True);
            Assert.That(_sessionStore.RevokedAllUserIds, Contains.Item(userId));
        });
    }

    [Test]
    public async Task DeleteUserAsync_ReturnsFailure_WhenDeletingSelf()
    {
        var userId = Id<User>.New();
        _userRepository.Users.Add(new User { Id = (Domain.ValueObjects.Id<User>)userId, Name = "Test", Email = new Email("test@test.com") });

        var result = await _service.DeleteUserAsync(userId, userId);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.InstanceOf<ForbiddenError>());
        });
    }

    [Test]
    public async Task UpdateUserAsync_UpdatesFields()
    {
        var userId = Id<User>.New();
        _userRepository.Users.Add(new User { Id = (Domain.ValueObjects.Id<User>)userId, Name = "Old", Email = new Email("old@test.com") });

        var command = new UpdateUserCommand { Name = "New", Email = "new@test.com", IsVisibleInRanking = false };
        var result = await _service.UpdateUserAsync(userId, Id<User>.New(), command);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            var updated = _userRepository.Users.First(u => u.Id == userId);
            Assert.That(updated.Name, Is.EqualTo("New"));
            Assert.That(updated.IsVisibleInRanking, Is.False);
        });
    }

    [Test]
    public async Task UpdateUserAsync_ReturnsConflict_WhenEmailAlreadyTaken()
    {
        var userId = Id<User>.New();
        var otherId = Id<User>.New();
        _userRepository.Users.Add(new User { Id = (Domain.ValueObjects.Id<User>)userId, Name = "Test", Email = new Email("test@test.com") });
        _userRepository.Users.Add(new User { Id = (Domain.ValueObjects.Id<User>)otherId, Name = "Other", Email = new Email("other@test.com") });

        var command = new UpdateUserCommand { Name = "Test", Email = "other@test.com" };
        var result = await _service.UpdateUserAsync(userId, Id<User>.New(), command);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.InstanceOf<ConflictError>());
        });
    }

    [Test]
    public async Task GetUsersAsync_ReturnsPaginatedResults()
    {
        _userRepository.Users.Add(new User { Id = (Domain.ValueObjects.Id<User>)Id<User>.New(), Name = "User1", Email = new Email("u1@test.com") });
        _userRepository.Users.Add(new User { Id = (Domain.ValueObjects.Id<User>)Id<User>.New(), Name = "User2", Email = new Email("u2@test.com") });

        var result = await _service.GetUsersAsync(new FilterInput { Page = 1, PageSize = 10 }, includeDeleted: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Items, Has.Count.EqualTo(2));
            Assert.That(result.Value.TotalCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task GetUsersAsync_WithIncludeDeleted_ReturnsDeletedUsers()
    {
        _userRepository.Users.Add(new User { Id = (Domain.ValueObjects.Id<User>)Id<User>.New(), Name = "Active", Email = new Email("active@test.com"), IsDeleted = false });
        _userRepository.Users.Add(new User { Id = (Domain.ValueObjects.Id<User>)Id<User>.New(), Name = "Deleted", Email = new Email("deleted@test.com"), IsDeleted = true });

        var resultWithDeleted = await _service.GetUsersAsync(new FilterInput { Page = 1, PageSize = 10 }, includeDeleted: true);

        Assert.Multiple(() =>
        {
            Assert.That(resultWithDeleted.IsSuccess, Is.True);
            Assert.That(resultWithDeleted.Value.Items, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task GetUserAsync_ReturnsDeletedUser_WhenIncludeDeleted()
    {
        var userId = Id<User>.New();
        _userRepository.Users.Add(new User { Id = (Domain.ValueObjects.Id<User>)userId, Name = "Deleted", Email = new Email("deleted@test.com"), IsDeleted = true });

        var result = await _service.GetUserAsync(userId);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.IsDeleted, Is.True);
        });
    }

    [Test]
    public async Task UpdateUserAsync_ReturnsNotFound_WhenUserNotFound()
    {
        var command = new UpdateUserCommand { Name = "Test", Email = "test@test.com" };
        var result = await _service.UpdateUserAsync(Id<User>.New(), Id<User>.New(), command);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.InstanceOf<NotFoundError>());
        });
    }

    [Test]
    public async Task DeleteUserAsync_ReturnsNotFound_WhenUserNotFound()
    {
        var result = await _service.DeleteUserAsync(Id<User>.New(), Id<User>.New());

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.InstanceOf<NotFoundError>());
        });
    }

    [Test]
    public async Task BlockUserAsync_ReturnsNotFound_WhenUserNotFound()
    {
        var result = await _service.BlockUserAsync(Id<User>.New(), Id<User>.New());

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.InstanceOf<NotFoundError>());
        });
    }

    [Test]
    public async Task UnblockUserAsync_ReturnsNotFound_WhenUserNotFound()
    {
        var result = await _service.UnblockUserAsync(Id<User>.New());

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.InstanceOf<NotFoundError>());
        });
    }

    [Test]
    public async Task UpdateUserAsync_ReturnsInvalidAdminUserError_WhenTargetUserIdIsEmpty()
    {
        var command = new UpdateUserCommand { Name = "Test", Email = "test@test.com" };
        var result = await _service.UpdateUserAsync(Id<User>.Empty, Id<User>.New(), command);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.InstanceOf<InvalidAdminUserError>());
        });
    }

    private sealed class InMemoryAdminUserRepository : IUserRepository
    {
        public List<User> Users { get; } = new();

        public Task<User?> FindByIdAsync(Id<User> id, CancellationToken cancellationToken = default)
            => Task.FromResult(Users.FirstOrDefault(u => u.Id == id && !u.IsDeleted));

        public Task<User?> FindByIdIncludingDeletedAsync(Id<User> id, CancellationToken cancellationToken = default)
            => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

        public Task<User?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(Users.FirstOrDefault(u => u.Name == name));

        public Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default)
            => Task.FromResult(Users.FirstOrDefault(u => u.Email == email));

        public Task<User?> FindByNameOrEmailAsync(string name, string email, CancellationToken cancellationToken = default)
            => Task.FromResult(Users.FirstOrDefault(u => u.Name == name || u.Email == email));

        public Task<List<UserRankingEntry>> GetRankingAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<UserRankingEntry>());

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            Users.Add(user);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
        {
            var index = Users.FindIndex(u => u.Id == user.Id);
            if (index >= 0) Users[index] = user;
            return Task.CompletedTask;
        }

        public Task<Pagination<UserResult>> GetUsersPaginatedAsync(FilterInput filterInput, bool includeDeleted, CancellationToken cancellationToken = default)
        {
            var query = Users.AsQueryable();
            if (!includeDeleted) query = query.Where(u => !u.IsDeleted);

            var items = query.Select(u => new UserResult
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                Avatar = u.Avatar,
                ProfileRank = u.ProfileRank,
                IsVisibleInRanking = u.IsVisibleInRanking,
                IsBlocked = u.IsBlocked,
                IsDeleted = u.IsDeleted,
                CreatedAt = u.CreatedAt
            }).ToList();

            return Task.FromResult(new Pagination<UserResult>
            {
                Items = items,
                Page = filterInput.Page,
                PageSize = filterInput.PageSize,
                TotalCount = items.Count
            });
        }
    }

    private sealed class InMemoryAdminRoleRepository : IRoleRepository
    {
        public List<Role> Roles { get; } = new();
        public Dictionary<Id<User>, List<Id<Role>>> UserRoles { get; } = new();

        public Task<List<Role>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Roles.OrderBy(r => r.Name).ToList());

        public Task<Role?> FindByIdAsync(Id<Role> roleId, CancellationToken cancellationToken = default)
            => Task.FromResult(Roles.FirstOrDefault(r => r.Id == roleId));

        public Task<Role?> FindByNameAsync(string roleName, CancellationToken cancellationToken = default)
            => Task.FromResult(Roles.FirstOrDefault(r => r.Name == roleName));

        public Task<List<Role>> GetByNamesAsync(IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default)
        {
            var normalized = roleNames.Select(n => n.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(Roles.Where(r => normalized.Contains(r.Name)).ToList());
        }

        public Task<bool> ExistsByNameAsync(string roleName, Id<Role>? excludeRoleId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Roles.Any(r => string.Equals(r.Name, roleName, StringComparison.OrdinalIgnoreCase)));

        public Task<List<string>> GetRoleNamesByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default)
        {
            if (!UserRoles.TryGetValue(userId, out var roleIds)) return Task.FromResult(new List<string>());
            return Task.FromResult(Roles.Where(r => roleIds.Contains(r.Id)).Select(r => r.Name).OrderBy(n => n).ToList());
        }

        public Task<Dictionary<Id<User>, List<string>>> GetRoleNamesByUserIdsAsync(IReadOnlyCollection<Id<User>> userIds, CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<Id<User>, List<string>>();
            foreach (var userId in userIds)
            {
                if (UserRoles.TryGetValue(userId, out var roleIds))
                {
                    result[userId] = Roles.Where(r => roleIds.Contains(r.Id)).Select(r => r.Name).OrderBy(n => n).ToList();
                }
            }
            return Task.FromResult(result);
        }

        public Task<List<string>> GetPermissionClaimsByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<string>());

        public Task<List<string>> GetPermissionClaimsByRoleIdAsync(Id<Role> targetRoleId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<string>());

        public Task<Dictionary<Id<Role>, List<string>>> GetPermissionClaimsByRoleIdsAsync(IReadOnlyCollection<Id<Role>> targetRoleIds, CancellationToken cancellationToken = default)
            => Task.FromResult(new Dictionary<Id<Role>, List<string>>());

        public Task<bool> UserHasRoleAsync(Id<User> userId, string roleName, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> UserHasPermissionAsync(Id<User> userId, string permission, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task AddRoleAsync(Role role, CancellationToken cancellationToken = default)
        {
            Roles.Add(role);
            return Task.CompletedTask;
        }

        public Task UpdateRoleAsync(Role role, CancellationToken cancellationToken = default)
        {
            var index = Roles.FindIndex(r => r.Id == role.Id);
            if (index >= 0) Roles[index] = role;
            return Task.CompletedTask;
        }

        public Task DeleteRoleAsync(Role role, CancellationToken cancellationToken = default)
        {
            Roles.RemoveAll(r => r.Id == role.Id);
            return Task.CompletedTask;
        }

        public Task ReplaceRolePermissionClaimsAsync(Id<Role> targetRoleId, IReadOnlyCollection<string> permissionClaims, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AddUserRolesAsync(Id<User> userId, IReadOnlyCollection<Id<Role>> roleIds, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ReplaceUserRolesAsync(Id<User> userId, IReadOnlyCollection<Id<Role>> roleIds, CancellationToken cancellationToken = default)
        {
            UserRoles[userId] = roleIds.Distinct().ToList();
            return Task.CompletedTask;
        }

        public Task<Pagination<Role>> GetRolesPaginatedAsync(FilterInput filterInput, CancellationToken cancellationToken = default)
            => Task.FromResult(new Pagination<Role>
            {
                Items = Roles.OrderBy(r => r.Name).Skip((filterInput.Page - 1) * filterInput.PageSize).Take(filterInput.PageSize).ToList(),
                Page = filterInput.Page,
                PageSize = filterInput.PageSize,
                TotalCount = Roles.Count
            });
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            return Task.FromResult(1);
        }
        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IUnitOfWorkTransaction>(new FakeTransaction());
        public void DetachEntity<TEntity>(TEntity entity) where TEntity : class { }
    }

    private sealed class FakeTransaction : IUnitOfWorkTransaction
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
