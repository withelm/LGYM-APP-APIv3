using LgymApi.Api.Middleware;
using LgymApi.Application.Models;
using LgymApi.Application.Pagination;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class UserContextMiddlewareBlockedTests
{
    private InMemoryBlockedUserRepository _userRepository = null!;
    private FakeBlockedSessionCache _sessionCache = null!;
    private UserContextMiddleware _middleware = null!;

    [SetUp]
    public void SetUp()
    {
        _userRepository = new InMemoryBlockedUserRepository();
        _sessionCache = new FakeBlockedSessionCache();
        _middleware = new UserContextMiddleware(context => Task.CompletedTask);
    }

    [Test]
    public async Task InvokeAsync_Returns403_WhenUserIsBlocked()
    {
        var userId = Id<User>.New();
        var user = new User
        {
            Id = (Domain.ValueObjects.Id<User>)userId,
            Name = "BlockedUser",
            Email = new Email("blocked@test.com"),
            IsBlocked = true
        };
        _userRepository.Users.Add(user);
        _sessionCache.AddOrRefresh(userId);

        var context = CreateHttpContext(userId);

        await _middleware.InvokeAsync(context, _userRepository, _sessionCache);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        });
    }

    [Test]
    public async Task InvokeAsync_PassesThrough_WhenUserIsNotBlocked()
    {
        var userId = Id<User>.New();
        var user = new User
        {
            Id = (Domain.ValueObjects.Id<User>)userId,
            Name = "ActiveUser",
            Email = new Email("active@test.com"),
            IsBlocked = false
        };
        _userRepository.Users.Add(user);
        _sessionCache.AddOrRefresh(userId);

        var nextCalled = false;
        var middleware = new UserContextMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateHttpContext(userId);

        await middleware.InvokeAsync(context, _userRepository, _sessionCache);

        Assert.Multiple(() =>
        {
            Assert.That(nextCalled, Is.True);
            Assert.That(context.Response.StatusCode, Is.EqualTo(200));
        });
    }

    private static DefaultHttpContext CreateHttpContext(Id<User> userId)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("userId", userId.ToString())
        }));
        context.Response.Body = new System.IO.MemoryStream();
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            EndpointMetadataCollection.Empty,
            "Test"));
        return context;
    }

    private sealed class InMemoryBlockedUserRepository : IUserRepository
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
        public Task<List<LgymApi.Application.Models.UserRankingEntry>> GetRankingAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<LgymApi.Application.Models.UserRankingEntry>());
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
        public Task<LgymApi.Application.Pagination.Pagination<LgymApi.Application.Repositories.AdminUserListItem>> GetUsersPaginatedAsync(LgymApi.Application.Pagination.FilterInput filterInput, bool includeDeleted, CancellationToken cancellationToken = default)
            => Task.FromResult(new LgymApi.Application.Pagination.Pagination<LgymApi.Application.Repositories.AdminUserListItem>());
    }

    private sealed class FakeBlockedSessionCache : IUserSessionCache
    {
        private readonly HashSet<Id<User>> _users = new();
        public void AddOrRefresh(Id<User> userId) => _users.Add(userId);
        public bool Remove(Id<User> userId) => _users.Remove(userId);
        public bool Contains(Id<User> userId) => _users.Contains(userId);
        public int Count => _users.Count;
    }
}
