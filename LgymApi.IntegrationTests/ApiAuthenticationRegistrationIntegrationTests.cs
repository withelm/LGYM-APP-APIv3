using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class ApiAuthenticationRegistrationIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task Hub_QueryToken_AuthenticatesValidBearerToken()
    {
        var user = await SeedUserAsync(name: "hub-query-token-user", email: "hub-query-token@example.com");
        SetAuthorizationHeader(user.Id);
        var token = Client.DefaultRequestHeaders.Authorization?.Parameter;
        token.Should().NotBeNullOrWhiteSpace();
        ClearAuthorizationHeader();

        var response = await Client.PostAsync(
            $"/hubs/notifications/negotiate?negotiateVersion=1&access_token={Uri.EscapeDataString(token!)}",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task ProtectedRoleRoute_WithRevokedSession_ReturnsUnauthorized()
    {
        // Given
        var administrator = await SeedUserAsync(
            name: "revoked-session-admin",
            email: "revoked-session-admin@example.com",
            isAdmin: true);
        SetAuthorizationHeader(administrator.Id);
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await db.UserSessions.SingleAsync(candidate => candidate.UserId == administrator.Id);
            session.RevokedAtUtc = DateTimeOffset.UnixEpoch;
            await db.SaveChangesAsync();
        }

        // When
        var response = await Client.GetAsync("/api/roles");
        var body = await response.Content.ReadFromJsonAsync<MiddlewareErrorResponse>();

        // Then
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        body.Should().NotBeNull();
        body!.Message.Should().Be("Unauthorized");
    }

    [Test]
    public async Task ProtectedRoleRoute_AfterPersistedDemotion_RejectsThePreIssuedToken()
    {
        var administrator = await SeedUserAsync(
            name: "stale-token-admin",
            email: "stale-token-admin@example.com",
            password: "pass1234",
            isAdmin: true);
        var login = await Client.PostAsJsonAsync("/api/login", new { name = administrator.Name, password = "pass1234" });
        var loginBody = await login.Content.ReadFromJsonAsync<LoginResponse>();
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        loginBody!.Token.Should().NotBeNullOrWhiteSpace();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody.Token);

        var beforeDemotion = await Client.GetAsync("/api/roles");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var adminRoleIds = await db.UserRoles
                .Where(candidate => candidate.UserId == administrator.Id && candidate.Role.Name == AuthConstants.Roles.Admin)
                .Select(candidate => candidate.RoleId)
                .ToListAsync();
            db.UserRoles.RemoveRange(db.UserRoles.Where(candidate =>
                candidate.UserId == administrator.Id && adminRoleIds.Contains(candidate.RoleId)));
            await db.SaveChangesAsync();
        }

        var afterDemotion = await Client.GetAsync("/api/roles");

        beforeDemotion.StatusCode.Should().Be(HttpStatusCode.OK);
        afterDemotion.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task ProtectedRoleRoute_WithBlockedAccount_ReturnsForbidden()
    {
        var administrator = await SeedUserAsync(
            name: "blocked-admin",
            email: "blocked-admin@example.com",
            isAdmin: true);
        SetAuthorizationHeader(administrator.Id);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var persistedAdministrator = await db.Users.FindAsync(administrator.Id);
            persistedAdministrator!.IsBlocked = true;
            await db.SaveChangesAsync();
        }

        var response = await Client.GetAsync("/api/roles");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task ProtectedRoleRoute_WithMismatchedAccountAndSession_ReturnsUnauthorized()
    {
        var claimedAdministrator = await SeedUserAsync(
            name: "claimed-admin",
            email: "claimed-admin@example.com",
            isAdmin: true);
        var sessionOwner = await SeedUserAsync(name: "session-owner", email: "session-owner@example.com");
        SetAuthorizationHeader(sessionOwner.Id);
        UserSession session;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            session = await db.UserSessions.SingleAsync(candidate => candidate.UserId == sessionOwner.Id);
        }
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateJwt(claimedAdministrator.Id, session.Id, session.Jti));

        var response = await Client.GetAsync("/api/roles");
        var body = await response.Content.ReadFromJsonAsync<MiddlewareErrorResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        body!.Message.Should().Be("Unauthorized");
    }

    [Test]
    public async Task ProtectedRoleRoute_WithLegitimateMultipleRolesAndCurrentPermission_ReturnsOk()
    {
        var administrator = await SeedUserAsync(
            name: "multi-role-admin",
            email: "multi-role-admin@example.com",
            isAdmin: true,
            additionalRoles: ["Trainer"]);
        SetAuthorizationHeader(administrator.Id);

        var response = await Client.GetAsync("/api/roles");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public void ShortJwtSigningKey_PreventsTestingHostStartup()
    {
        using var baseFactory = new CustomWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.UseSetting("Jwt:SigningKey", new string('x', 31)));

        var startHost = () =>
        {
            using var client = factory.CreateClient();
        };

        startHost.Should().Throw<Exception>()
            .Which.ToString().Should().Contain("Jwt:SigningKey is not configured or is too short.");
    }

    private sealed class MiddlewareErrorResponse
    {
        [JsonPropertyName("msg")]
        public string Message { get; set; } = string.Empty;
    }

    private sealed class LoginResponse
    {
        [JsonPropertyName("token")]
        public string? Token { get; set; }
    }
}
