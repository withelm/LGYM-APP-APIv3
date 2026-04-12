using FluentAssertions;
using LgymApi.Api.Hubs;
using LgymApi.Domain.Security;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using NUnit.Framework;

namespace LgymApi.UnitTests.InAppNotifications;

[TestFixture]
public sealed class NotificationHubUserIdProviderTests
{
    [Test]
    public void GetUserId_WithUserIdClaim_ReturnsClaimValue()
    {
        var connection = CreateConnection(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AuthConstants.ClaimNames.UserId, "user-123") }, "Test")));

        var result = new NotificationHubUserIdProvider().GetUserId(connection);

        result.Should().Be("user-123");
    }

    [Test]
    public void GetUserId_WithMissingUser_ReturnsNull()
    {
        var connection = CreateConnection(null);

        var result = new NotificationHubUserIdProvider().GetUserId(connection);

        result.Should().BeNull();
    }

    [Test]
    public void GetUserId_WithMissingClaim_ReturnsNull()
    {
        var connection = CreateConnection(new ClaimsPrincipal(new ClaimsIdentity()));

        var result = new NotificationHubUserIdProvider().GetUserId(connection);

        result.Should().BeNull();
    }

    private static HubConnectionContext CreateConnection(ClaimsPrincipal? user)
    {
        var context = new DefaultConnectionContext("connection-1") { User = user };
        return new HubConnectionContext(context, new HubConnectionContextOptions(), NullLoggerFactory.Instance);
    }
}
