using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class UserSessionIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task Login_WithValidCredentials_CreatesPersistentSessionRowInDatabase()
    {
        var user = await SeedUserAsync(name: "session-login", email: "session-login@example.com", password: "login-pass-123");

        var login = await LoginAsync("session-login", "login-pass-123");
        var sessionClaims = ReadSessionClaims(login.Token);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = await db.UserSessions.SingleAsync(s => s.Id == sessionClaims.SessionId);

        Assert.That(session.UserId, Is.EqualTo(user.Id));
        Assert.That(session.Jti, Is.EqualTo(sessionClaims.Jti));
        Assert.That(session.RevokedAtUtc, Is.Null);
        Assert.That(session.ExpiresAtUtc, Is.GreaterThan(DateTimeOffset.UtcNow.AddHours(12)));
    }

    [Test]
    public async Task Logout_RevokesCurrentSession_AndSubsequentRequestWithSameTokenIsRejected()
    {
        await SeedUserAsync(name: "session-logout", email: "session-logout@example.com", password: "logout-pass-123");

        var login = await LoginAsync("session-logout", "logout-pass-123");
        var sessionClaims = ReadSessionClaims(login.Token);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);

        var logoutResponse = await Client.PostAsync("/api/logout", null);
        Assert.That(logoutResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await db.UserSessions.SingleAsync(s => s.Id == sessionClaims.SessionId);
            Assert.That(session.RevokedAtUtc, Is.Not.Null);
        }

        var checkTokenResponse = await Client.GetAsync("/api/checkToken");
        Assert.That(checkTokenResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var body = await checkTokenResponse.Content.ReadFromJsonAsync<MiddlewareErrorResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo("Unauthorized"));
    }

    [Test]
    public async Task ResetPassword_OnSuccess_RevokesAllExistingUserSessions()
    {
        var user = await SeedUserAsync(name: "session-reset", email: "session-reset@example.com", password: "reset-old-pass-123");
        var firstSession = await CreatePersistedSessionAsync(user.Id);
        var secondSession = await CreatePersistedSessionAsync(user.Id);

        await SeedPasswordResetTokenAsync(user.Id, "RESET-SESSION-TOKEN-001", DateTimeOffset.UtcNow.AddMinutes(30), isUsed: false);

        var resetResponse = await Client.PostAsJsonAsync("/api/reset-password", new
        {
            token = "RESET-SESSION-TOKEN-001",
            newPassword = "reset-new-pass-123",
            confirmPassword = "reset-new-pass-123"
        });
        Assert.That(resetResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var sessions = await db.UserSessions
                .Where(s => s.UserId == user.Id)
                .OrderBy(s => s.CreatedAt)
                .ToListAsync();

            Assert.That(sessions, Has.Exactly(2).Items);
            Assert.That(sessions.Select(s => s.Id), Is.EquivalentTo(new[] { firstSession.SessionId, secondSession.SessionId }));
            Assert.That((sessions).All(s => s.RevokedAtUtc != null), Is.True);
        }

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstSession.Token);
        var rejectedResponse = await Client.GetAsync("/api/checkToken");
        Assert.That(rejectedResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var rejectedBody = await rejectedResponse.Content.ReadFromJsonAsync<MiddlewareErrorResponse>();
        Assert.That(rejectedBody, Is.Not.Null);
        Assert.That(rejectedBody!.Message, Is.EqualTo("Unauthorized"));
    }

    [Test]
    public async Task CheckToken_WithManuallyRevokedSession_ReturnsUnauthorized()
    {
        var user = await SeedUserAsync(name: "session-revoked", email: "session-revoked@example.com", password: "revoked-pass-123");
        var sessionClaims = await CreatePersistedSessionAsync(user.Id);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await db.UserSessions.SingleAsync(s => s.Id == sessionClaims.SessionId);
            session.RevokedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessionClaims.Token);
        var response = await Client.GetAsync("/api/checkToken");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var body = await response.Content.ReadFromJsonAsync<MiddlewareErrorResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo("Unauthorized"));
    }

    [Test]
    public async Task CheckToken_WithExpiredSession_ReturnsUnauthorized()
    {
        var user = await SeedUserAsync(name: "session-expired", email: "session-expired@example.com", password: "expired-pass-123");
        var sessionClaims = await CreatePersistedSessionAsync(user.Id, DateTimeOffset.UtcNow.AddDays(1));

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await db.UserSessions.SingleAsync(s => s.Id == sessionClaims.SessionId);
            session.ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessionClaims.Token);
        var response = await Client.GetAsync("/api/checkToken");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var body = await response.Content.ReadFromJsonAsync<MiddlewareErrorResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo("Unauthorized"));
    }

    [TestCase("block")]
    [TestCase("delete")]
    public async Task AdminUserMutation_RevokesTargetUserSessions_AndRejectsExistingToken(string action)
    {
        var admin = await SeedAdminAsync();
        var targetUser = await SeedUserAsync(name: $"session-admin-{action}", email: $"session-admin-{action}@example.com", password: "admin-pass-123");

        var adminSession = await CreatePersistedSessionAsync(admin.Id);
        var targetSessionClaims = await CreatePersistedSessionAsync(targetUser.Id);

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminSession.Token);
        var adminResponse = await Client.PostAsync($"/api/admin/users/{targetUser.Id}/{action}", null);
        Assert.That(adminResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await db.UserSessions.SingleAsync(s => s.Id == targetSessionClaims.SessionId);
            Assert.That(session.UserId, Is.EqualTo(targetUser.Id));
            Assert.That(session.RevokedAtUtc, Is.Not.Null);

            var updatedUser = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == targetUser.Id);
            if (action == "block")
            {
                Assert.That(updatedUser.IsBlocked, Is.True);
                Assert.That(updatedUser.IsDeleted, Is.False);
            }
            else
            {
                Assert.That(updatedUser.IsDeleted, Is.False);
                Assert.That(updatedUser.DeletionState, Is.EqualTo(LgymApi.Domain.Enums.AccountDeletionState.PendingDeletion));
                Assert.That(updatedUser.Name, Is.EqualTo(targetUser.Name));
                Assert.That(updatedUser.Email, Is.EqualTo(targetUser.Email));
            }
        }

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", targetSessionClaims.Token);
        var rejectedResponse = await Client.GetAsync("/api/checkToken");
        Assert.That(rejectedResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var rejectedBody = await rejectedResponse.Content.ReadFromJsonAsync<MiddlewareErrorResponse>();
        Assert.That(rejectedBody, Is.Not.Null);
        Assert.That(rejectedBody!.Message, Is.EqualTo("Unauthorized"));
    }

    private async Task<LoginResponse> LoginAsync(string name, string password)
    {
        var response = await Client.PostAsJsonAsync("/api/login", new { name, password });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(string.IsNullOrWhiteSpace(body!.Token), Is.False);
        return body;
    }

    private async Task<SessionClaims> CreatePersistedSessionAsync(Id<User> userId, DateTimeOffset? expiresAtUtc = null)
    {
        var sessionId = Id<UserSession>.New();
        var jti = Id<UserSession>.New().ToString();

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserSessions.Add(new UserSession
            {
                Id = sessionId,
                UserId = userId,
                Jti = jti,
                ExpiresAtUtc = expiresAtUtc ?? DateTimeOffset.UtcNow.AddDays(1)
            });

            await db.SaveChangesAsync();
        }

        var token = GenerateJwt(userId, sessionId, jti);
        return new SessionClaims(sessionId, jti, token);
    }

    private SessionClaims ReadSessionClaims(string token)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var sid = jwt.Claims.Single(c => c.Type == AuthConstants.ClaimNames.SessionId).Value;
        var jti = jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        Assert.That(Id<UserSession>.TryParse(sid, out var sessionId), Is.True);

        return new SessionClaims(
            SessionId: sessionId,
            Jti: jti,
            Token: token);
    }

    private async Task SeedPasswordResetTokenAsync(
        LgymApi.Domain.ValueObjects.Id<User> userId,
        string plainTextToken,
        DateTimeOffset expiresAt,
        bool isUsed)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = userId,
            TokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(plainTextToken))),
            ExpiresAt = expiresAt,
            IsUsed = isUsed
        });

        await db.SaveChangesAsync();
    }

    private sealed record SessionClaims(
        LgymApi.Domain.ValueObjects.Id<UserSession> SessionId,
        string Jti,
        string Token);

    private sealed class LoginResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;
    }

    private sealed class MiddlewareErrorResponse
    {
        [JsonPropertyName("msg")]
        public string Message { get; set; } = string.Empty;
    }
}
