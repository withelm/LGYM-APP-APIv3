using LgymApi.Api.Hubs;
using LgymApi.Application.Services;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace LgymApi.UnitTests.InAppNotifications;

[TestFixture]
public sealed class NotificationHubTests
{
    [Test]
    public async Task OnConnectedAsync_ValidUser_AddsUserToGroup()
    {
        var userId = Id<User>.New().GetValue();
        var context = new TestHubCallerContext(userId.ToString());
        var sessionCache = new FakeUserSessionCache(true);
        var groups = new FakeGroupManager();

        var hub = new NotificationHub(sessionCache)
        {
            Context = context,
            Groups = groups
        };

        await hub.OnConnectedAsync();

        Assert.That(context.AbortCalled, Is.False);
        Assert.That(sessionCache.ContainsCalls, Is.EqualTo(1));
        Assert.That(groups.AddCalls, Is.EqualTo(1));
        Assert.That(groups.LastGroupName, Is.EqualTo($"user-{userId}"));
    }

    [Test]
    public async Task OnConnectedAsync_MissingUserId_AbortsConnection()
    {
        var context = new TestHubCallerContext(null);
        var sessionCache = new FakeUserSessionCache(true);
        var groups = new FakeGroupManager();

        var hub = new NotificationHub(sessionCache)
        {
            Context = context,
            Groups = groups
        };

        await hub.OnConnectedAsync();

        Assert.That(context.AbortCalled, Is.True);
        Assert.That(sessionCache.ContainsCalls, Is.EqualTo(0));
        Assert.That(groups.AddCalls, Is.EqualTo(0));
    }

    [Test]
    public async Task OnConnectedAsync_UserNotInSessionCache_AbortsConnection()
    {
        var userId = Id<User>.New().GetValue();
        var context = new TestHubCallerContext(userId.ToString());
        var sessionCache = new FakeUserSessionCache(false);
        var groups = new FakeGroupManager();

        var hub = new NotificationHub(sessionCache)
        {
            Context = context,
            Groups = groups
        };

        await hub.OnConnectedAsync();

        Assert.That(context.AbortCalled, Is.True);
        Assert.That(sessionCache.ContainsCalls, Is.EqualTo(1));
        Assert.That(groups.AddCalls, Is.EqualTo(0));
    }

    private sealed class FakeUserSessionCache : IUserSessionCache
    {
        private readonly bool _containsResult;

        public FakeUserSessionCache(bool containsResult) => _containsResult = containsResult;

        public int ContainsCalls { get; private set; }

        public void AddOrRefresh(Id<User> userId) { }
        public bool Remove(Id<User> userId) => true;
        public bool Contains(Id<User> userId)
        {
            ContainsCalls++;
            return _containsResult;
        }
        public int Count => 0;
    }

    private sealed class FakeGroupManager : IGroupManager
    {
        public int AddCalls { get; private set; }
        public string? LastGroupName { get; private set; }

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            AddCalls++;
            LastGroupName = groupName;
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class TestHubCallerContext : HubCallerContext
    {
        public TestHubCallerContext(string? userId)
        {
            if (userId is null)
            {
                User = new ClaimsPrincipal(new ClaimsIdentity());
            }
            else
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("userId", userId) }, "Test"));
            }
        }

        public bool AbortCalled { get; private set; }

        public override string ConnectionId { get; } = "connection-1";
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal? User { get; }
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() => AbortCalled = true;
    }
}
