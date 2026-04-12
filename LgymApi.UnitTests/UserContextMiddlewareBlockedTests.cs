using LgymApi.Application.Features.AdminManagement.Models;
using LgymApi.Api.Middleware;
using LgymApi.Application.Models;
using LgymApi.Application.Pagination;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.TestUtils.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using FluentAssertions;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class UserContextMiddlewareBlockedTests
{
    private InMemoryBlockedUserRepository _userRepository = null!;
    private FakeUserSessionStore _sessionStore = null!;
    private UserContextMiddleware _middleware = null!;

    [SetUp]
    public void SetUp()
    {
        _userRepository = new InMemoryBlockedUserRepository();
        _sessionStore = new FakeUserSessionStore();
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
        var session = await _sessionStore.CreateSessionAsync(userId, DateTimeOffset.UtcNow.AddDays(30), CancellationToken.None);

        var context = CreateHttpContext(userId, session.Id);

        await _middleware.InvokeAsync(context, _userRepository, _sessionStore);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
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
        var session = await _sessionStore.CreateSessionAsync(userId, DateTimeOffset.UtcNow.AddDays(30), CancellationToken.None);

        var nextCalled = false;
        var middleware = new UserContextMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateHttpContext(userId, session.Id);

        await middleware.InvokeAsync(context, _userRepository, _sessionStore);

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }

    private static DefaultHttpContext CreateHttpContext(Id<User> userId, Id<UserSession> sessionId)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AuthConstants.ClaimNames.UserId, userId.ToString()),
            new Claim(AuthConstants.ClaimNames.SessionId, sessionId.ToString())
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
        public Task<LgymApi.Application.Pagination.Pagination<LgymApi.Application.Features.AdminManagement.Models.UserResult>> GetUsersPaginatedAsync(LgymApi.Application.Pagination.FilterInput filterInput, bool includeDeleted, CancellationToken cancellationToken = default)
            => Task.FromResult(new LgymApi.Application.Pagination.Pagination<LgymApi.Application.Features.AdminManagement.Models.UserResult>());
    }
}
