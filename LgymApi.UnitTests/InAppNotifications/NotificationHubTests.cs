using LgymApi.Api.Hubs;
using LgymApi.Application.Services;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.UnitTests.Fakes;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;

namespace LgymApi.UnitTests.InAppNotifications;

[TestFixture]
public sealed class NotificationHubTests
{
    [Test]
    public async Task OnConnectedAsync_ValidUser_AddsUserToGroup()
    {
        var userId = Id<User>.New();
        var sessionStore = new FakeUserSessionStore();
        var session = await sessionStore.CreateSessionAsync(userId, DateTimeOffset.UtcNow.AddDays(30), CancellationToken.None);
        var context = new TestHubCallerContext(userId.ToString(), session.Id.ToString());
        var groups = new FakeGroupManager();

        var hub = new NotificationHub(sessionStore, NullLogger<NotificationHub>.Instance)
        {
            Context = context,
            Groups = groups
        };

        await hub.OnConnectedAsync();

        Assert.That(context.AbortCalled, Is.False);
        Assert.That(groups.AddCalls, Is.EqualTo(1));
        Assert.That(groups.LastGroupName, Is.EqualTo($"user-{userId}"));
    }

    [Test]
    public async Task OnConnectedAsync_MissingSessionId_AbortsConnection()
    {
        var context = new TestHubCallerContext(Id<User>.New().ToString(), null);
        var sessionStore = new FakeUserSessionStore();
        var groups = new FakeGroupManager();

        var hub = new NotificationHub(sessionStore, NullLogger<NotificationHub>.Instance)
        {
            Context = context,
            Groups = groups
        };

        await hub.OnConnectedAsync();

        Assert.That(context.AbortCalled, Is.True);
        Assert.That(groups.AddCalls, Is.EqualTo(0));
    }

    [Test]
    public async Task OnConnectedAsync_InvalidSession_AbortsConnection()
    {
        var userId = Id<User>.New();
        var invalidSessionId = Id<UserSession>.New();
        var context = new TestHubCallerContext(userId.ToString(), invalidSessionId.ToString());
        var sessionStore = new FakeUserSessionStore();
        var groups = new FakeGroupManager();

        var hub = new NotificationHub(sessionStore, NullLogger<NotificationHub>.Instance)
        {
            Context = context,
            Groups = groups
        };

        await hub.OnConnectedAsync();

        Assert.That(context.AbortCalled, Is.True);
        Assert.That(groups.AddCalls, Is.EqualTo(0));
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
        public TestHubCallerContext(string? userId, string? sessionId)
        {
            var claims = new List<Claim>();

            if (userId != null)
            {
                claims.Add(new Claim(AuthConstants.ClaimNames.UserId, userId));
            }

            if (sessionId != null)
            {
                claims.Add(new Claim(AuthConstants.ClaimNames.SessionId, sessionId));
            }

            User = claims.Count > 0
                ? new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
                : new ClaimsPrincipal(new ClaimsIdentity());
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
