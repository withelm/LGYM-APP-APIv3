using System.Net;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Identity.Errors;
using LgymApi.Application.Features.AdminManagement;
using LgymApi.Application.Features.AdminManagement.Models;
using LgymApi.Application.Models;
using LgymApi.Application.Pagination;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.UnitTests.Fakes;
using LgymApi.TestUtils.Fakes;
using NSubstitute;
using NUnit.Framework;
using LgymApi.Identity.Contracts.AccountDeletion;
using LgymApi.Identity.Contracts;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class AdminUserServiceTests
{
    private AdminUserService _service = null!;
    private ConfigurableUserRepository _userRepository = null!;
    private ConfigurableRoleRepository _roleRepository = null!;
    private FakeUserSessionStore _sessionStore = null!;
    private IUnitOfWork _unitOfWork = null!;
    private int _saveChangesCalls;
    private IAccountDeletionService _accountDeletionService = null!;

    [SetUp]
    public void SetUp()
    {
        _userRepository = new ConfigurableUserRepository();
        _roleRepository = new ConfigurableRoleRepository();
        _sessionStore = new FakeUserSessionStore();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                _saveChangesCalls++;
                return Task.FromResult(1);
            });

        _accountDeletionService = Substitute.For<IAccountDeletionService>();
        _service = new AdminUserService(_userRepository, _roleRepository, _sessionStore, _unitOfWork, _accountDeletionService);
    }

    [Test]
    public async Task Should_ReturnUserWithRoles_When_UserExists()
    {
        var userId = Id<User>.New();
        var roleId = Id<Role>.New();
        var user = new User { Id = (Domain.ValueObjects.Id<User>)userId, Name = "Test", Email = new Email("test@test.com") };
        var role = new Role { Id = (Domain.ValueObjects.Id<Role>)roleId, Name = "Admin" };

        _userRepository.FindByIdIncludingDeleted = (_, _) => Task.FromResult<User?>(user);
        _roleRepository.GetRoleNamesByUserId = (_, _) => Task.FromResult(new List<string> { role.Name });

        var result = await _service.GetUserAsync(userId);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Name, Is.EqualTo("Test"));
        Assert.That(result.Value.Roles, Has.Exactly(1).Items);
        Assert.That(result.Value.Roles[0], Is.EqualTo("Admin"));
    }

    [Test]
    public async Task Should_ReturnFailure_When_UserNotFound()
    {
        var result = await _service.GetUserAsync(Id<User>.New());

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<NotFoundError>());
    }

    [Test]
    public async Task Should_BlockUserAndRevokeAllSessions_When_Called()
    {
        var userId = Id<User>.New();
        var adminId = Id<User>.New();
        var user = new User { Id = (Domain.ValueObjects.Id<User>)userId, Name = "Test", Email = new Email("test@test.com") };
        _userRepository.FindById = (_, _) => Task.FromResult<User?>(user);

        var result = await _service.BlockUserAsync(userId, adminId);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(user.IsBlocked, Is.True);
        Assert.That(_sessionStore.RevokedAllUserIds, Does.Contain(userId));
        Assert.That(_saveChangesCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task Should_ReturnFailure_When_BlockingSelf()
    {
        var userId = Id<User>.New();
        _userRepository.FindById = (_, _) => Task.FromResult<User?>(new User { Id = (Domain.ValueObjects.Id<User>)userId, Name = "Test", Email = new Email("test@test.com") });

        var result = await _service.BlockUserAsync(userId, userId);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<ForbiddenError>());
    }

    [Test]
    public async Task Should_UnblockUser_When_Called()
    {
        var userId = Id<User>.New();
        var user = new User { Id = (Domain.ValueObjects.Id<User>)userId, Name = "Test", Email = new Email("test@test.com"), IsBlocked = true };
        _userRepository.FindById = (_, _) => Task.FromResult<User?>(user);

        var result = await _service.UnblockUserAsync(userId);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(user.IsBlocked, Is.False);
    }

    [Test]
    public async Task Should_RequestCanonicalLifecycle_When_DeletingAnotherUser()
    {
        var userId = Id<User>.New();
        var adminId = Id<User>.New();
        var user = new User { Id = (Domain.ValueObjects.Id<User>)userId, Name = "Test", Email = new Email("test@test.com") };
        _userRepository.FindByIdIncludingDeleted = (_, _) => Task.FromResult<User?>(user);

        var result = await _service.DeleteUserAsync(userId, adminId);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(user.IsDeleted, Is.False);
        await _accountDeletionService.Received(1).RequestAsync(userId.Rebind<AccountReference>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Should_ReturnFailure_When_DeletingSelf()
    {
        var userId = Id<User>.New();
        _userRepository.FindById = (_, _) => Task.FromResult<User?>(new User { Id = (Domain.ValueObjects.Id<User>)userId, Name = "Test", Email = new Email("test@test.com") });

        var result = await _service.DeleteUserAsync(userId, userId);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<ForbiddenError>());
    }

    [Test]
    public async Task Should_UpdateFields_When_Called()
    {
        var userId = Id<User>.New();
        var user = new User { Id = (Domain.ValueObjects.Id<User>)userId, Name = "Old", Email = new Email("old@test.com") };
        _userRepository.FindByIdIncludingDeleted = (_, _) => Task.FromResult<User?>(user);
        _userRepository.FindByEmail = (_, _) => Task.FromResult<User?>(null);

        var command = new UpdateUserCommand { Name = "New", Email = "new@test.com", IsVisibleInRanking = false };
        var result = await _service.UpdateUserAsync(userId, Id<User>.New(), command);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(user.Name, Is.EqualTo("New"));
        Assert.That(user.IsVisibleInRanking, Is.False);
    }

    [Test]
    public async Task Should_ReturnConflict_When_EmailAlreadyTaken()
    {
        var userId = Id<User>.New();
        var otherId = Id<User>.New();
        var user = new User { Id = (Domain.ValueObjects.Id<User>)userId, Name = "Test", Email = new Email("test@test.com") };
        var otherUser = new User { Id = (Domain.ValueObjects.Id<User>)otherId, Name = "Other", Email = new Email("other@test.com") };

        _userRepository.FindByIdIncludingDeleted = (_, _) => Task.FromResult<User?>(user);
        _userRepository.FindByEmail = (_, _) => Task.FromResult<User?>(otherUser);

        var command = new UpdateUserCommand { Name = "Test", Email = "other@test.com" };
        var result = await _service.UpdateUserAsync(userId, Id<User>.New(), command);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<ConflictError>());
    }

    [Test]
    public async Task Should_ReturnPaginatedResults_When_Called()
    {
        var user1 = new User { Id = (Domain.ValueObjects.Id<User>)Id<User>.New(), Name = "User1", Email = new Email("u1@test.com") };
        var user2 = new User { Id = (Domain.ValueObjects.Id<User>)Id<User>.New(), Name = "User2", Email = new Email("u2@test.com") };

        _userRepository.GetUsersPaginated = (_, _, _) => Task.FromResult(new Pagination<UserResult>
        {
            Items = new List<UserResult>
            {
                new() { Id = user1.Id, Name = user1.Name, Email = user1.Email, Avatar = user1.Avatar, ProfileRank = user1.ProfileRank, IsVisibleInRanking = user1.IsVisibleInRanking, IsBlocked = user1.IsBlocked, IsDeleted = user1.IsDeleted, CreatedAt = user1.CreatedAt },
                new() { Id = user2.Id, Name = user2.Name, Email = user2.Email, Avatar = user2.Avatar, ProfileRank = user2.ProfileRank, IsVisibleInRanking = user2.IsVisibleInRanking, IsBlocked = user2.IsBlocked, IsDeleted = user2.IsDeleted, CreatedAt = user2.CreatedAt }
            },
            Page = 1,
            PageSize = 10,
            TotalCount = 2
        });
        _roleRepository.GetRoleNamesByUserIds = (_, _) => Task.FromResult(new Dictionary<Id<User>, List<string>>());

        var result = await _service.GetUsersAsync(new FilterInput { Page = 1, PageSize = 10 }, includeDeleted: false);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Items, Has.Exactly(2).Items);
        Assert.That(result.Value.TotalCount, Is.EqualTo(2));
    }

    [Test]
    public async Task Should_ReturnDeletedUsers_When_IncludeDeletedIsTrue()
    {
        var activeUser = new User { Id = (Domain.ValueObjects.Id<User>)Id<User>.New(), Name = "Active", Email = new Email("active@test.com"), IsDeleted = false };
        var deletedUser = new User { Id = (Domain.ValueObjects.Id<User>)Id<User>.New(), Name = "Deleted", Email = new Email("deleted@test.com"), IsDeleted = true };

        _userRepository.GetUsersPaginated = (_, _, _) => Task.FromResult(new Pagination<UserResult>
        {
            Items = new List<UserResult>
            {
                new() { Id = activeUser.Id, Name = activeUser.Name, Email = activeUser.Email, Avatar = activeUser.Avatar, ProfileRank = activeUser.ProfileRank, IsVisibleInRanking = activeUser.IsVisibleInRanking, IsBlocked = activeUser.IsBlocked, IsDeleted = activeUser.IsDeleted, CreatedAt = activeUser.CreatedAt },
                new() { Id = deletedUser.Id, Name = deletedUser.Name, Email = deletedUser.Email, Avatar = deletedUser.Avatar, ProfileRank = deletedUser.ProfileRank, IsVisibleInRanking = deletedUser.IsVisibleInRanking, IsBlocked = deletedUser.IsBlocked, IsDeleted = deletedUser.IsDeleted, CreatedAt = deletedUser.CreatedAt }
            },
            Page = 1,
            PageSize = 10,
            TotalCount = 2
        });
        _roleRepository.GetRoleNamesByUserIds = (_, _) => Task.FromResult(new Dictionary<Id<User>, List<string>>());

        var resultWithDeleted = await _service.GetUsersAsync(new FilterInput { Page = 1, PageSize = 10 }, includeDeleted: true);

        Assert.That(resultWithDeleted.IsSuccess, Is.True);
        Assert.That(resultWithDeleted.Value.Items, Has.Exactly(2).Items);
    }

    [Test]
    public async Task Should_ReturnDeletedUser_When_IncludeDeletedIsTrue()
    {
        var userId = Id<User>.New();
        var user = new User { Id = (Domain.ValueObjects.Id<User>)userId, Name = "Deleted", Email = new Email("deleted@test.com"), IsDeleted = true };
        _userRepository.FindByIdIncludingDeleted = (_, _) => Task.FromResult<User?>(user);
        _roleRepository.GetRoleNamesByUserId = (_, _) => Task.FromResult(new List<string>());

        var result = await _service.GetUserAsync(userId);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.IsDeleted, Is.True);
    }

    [Test]
    public async Task Should_ReturnNotFound_When_UserNotFoundForUpdate()
    {
        var command = new UpdateUserCommand { Name = "Test", Email = "test@test.com" };
        var result = await _service.UpdateUserAsync(Id<User>.New(), Id<User>.New(), command);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<NotFoundError>());
    }

    [Test]
    public async Task Should_ReturnNotFound_When_UserNotFoundForDelete()
    {
        var result = await _service.DeleteUserAsync(Id<User>.New(), Id<User>.New());

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<NotFoundError>());
    }

    [Test]
    public async Task Should_ReturnNotFound_When_UserNotFoundForBlock()
    {
        var result = await _service.BlockUserAsync(Id<User>.New(), Id<User>.New());

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<NotFoundError>());
    }

    [Test]
    public async Task Should_ReturnNotFound_When_UserNotFoundForUnblock()
    {
        var result = await _service.UnblockUserAsync(Id<User>.New());

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<NotFoundError>());
    }

    [Test]
    public async Task Should_ReturnInvalidAdminUserError_When_TargetUserIdIsEmpty()
    {
        var command = new UpdateUserCommand { Name = "Test", Email = "test@test.com" };
        var result = await _service.UpdateUserAsync(Id<User>.Empty, Id<User>.New(), command);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<InvalidAdminUserError>());
    }

    private static UserResult ToUserResult(User user) => new()
    {
        Id = user.Id,
        Name = user.Name,
        Email = user.Email,
        Avatar = user.Avatar,
        ProfileRank = user.ProfileRank,
        IsVisibleInRanking = user.IsVisibleInRanking,
        IsBlocked = user.IsBlocked,
        IsDeleted = user.IsDeleted,
        CreatedAt = user.CreatedAt
    };
}
