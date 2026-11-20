using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class PushInstallationApiTests : IntegrationTestBase
{
    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/push/installations/unregister", "own", "owner-allow")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/push/installations/register", "own", "owner-allow")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/push/installations/disassociate", "own", "owner-allow")]
    public async Task PushInstallationEndpoints_UseLegacyRoutesJsonFieldsAndMessageResponse()
    {
        var user = await SeedUserAsync(name: "push-wire", email: "push-wire@example.com", password: "push-pass-123");
        SetAuthorizationHeader(user.Id);

        var registerResponse = await Client.PostAsJsonAsync("/api/push/installations/register", new
        {
            installationId = "device-wire",
            platform = "android",
            fcmToken = "token-wire",
            appVersion = "1.0.0",
            environment = "development",
            permissionStatus = "authorized"
        });
        await AssertLegacyMessageResponseAsync(registerResponse);

        var unregisterResponse = await Client.PostAsJsonAsync("/api/push/installations/unregister", new
        {
            installationId = "device-wire"
        });
        await AssertLegacyMessageResponseAsync(unregisterResponse);

        var disassociateResponse = await Client.PostAsJsonAsync("/api/push/installations/disassociate", new
        {
            installationId = "device-wire"
        });
        await AssertLegacyMessageResponseAsync(disassociateResponse);
    }

    [Test]
    public async Task PushInstallationEndpoints_RejectBlankAndAlternateInstallationIdentifiers()
    {
        var user = await SeedUserAsync(name: "push-validation", email: "push-validation@example.com", password: "push-pass-123");
        SetAuthorizationHeader(user.Id);

        var blankRegistration = await Client.PostAsJsonAsync("/api/push/installations/register", new
        {
            installationId = "",
            platform = "android",
            fcmToken = "token-validation",
            environment = "development"
        });
        Assert.That(blankRegistration.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var alternateRegistration = await Client.PostAsJsonAsync("/api/push/installations/register", new
        {
            installationKey = "device-validation",
            platform = "android",
            fcmToken = "token-validation",
            environment = "development"
        });
        Assert.That(alternateRegistration.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        await Client.PostAsJsonAsync("/api/push/installations/register", new
        {
            installationId = "device-validation",
            platform = "android",
            fcmToken = "token-validation",
            environment = "development"
        });

        var blankUnregister = await Client.PostAsJsonAsync("/api/push/installations/unregister", new { installationId = "" });
        var alternateUnregister = await Client.PostAsJsonAsync("/api/push/installations/unregister", new { installationKey = "device-validation" });
        var blankDisassociate = await Client.PostAsJsonAsync("/api/push/installations/disassociate", new { installationId = "" });
        var alternateDisassociate = await Client.PostAsJsonAsync("/api/push/installations/disassociate", new { installationKey = "device-validation" });

        Assert.That(blankUnregister.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(alternateUnregister.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(blankDisassociate.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(alternateDisassociate.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var installation = await db.PushInstallations.SingleAsync(x => x.InstallationId == "device-validation");
        Assert.That(installation.UserId, Is.EqualTo(user.Id));
        Assert.That(installation.SessionId, Is.Not.Null);
        Assert.That(installation.DisabledAt, Is.Null);
    }

    [Test]
    public async Task RegisterPushInstallation_MissingTokenOrPlatform_ReturnsLocalizedBadRequestWithoutEchoingToken()
    {
        var user = await SeedUserAsync(name: "push-token-platform", email: "push-token-platform@example.com", password: "push-pass-123");
        SetAuthorizationHeader(user.Id);

        var missingToken = await Client.PostAsJsonAsync("/api/push/installations/register", new
        {
            installationId = "device-missing-token",
            platform = "android",
            fcmToken = "",
            environment = "development"
        });
        var missingPlatform = await Client.PostAsJsonAsync("/api/push/installations/register", new
        {
            installationId = "device-missing-platform",
            platform = "",
            fcmToken = "token-that-must-not-leak",
            environment = "development"
        });

        Assert.That(missingToken.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(missingPlatform.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await missingToken.Content.ReadAsStringAsync()), Does.Contain("All fields are required."));
        var assertedChain15 = (await missingPlatform.Content.ReadAsStringAsync());
        Assert.That(assertedChain15, Does.Contain("All fields are required."));
        Assert.That(assertedChain15, Does.Not.Contain("token-that-must-not-leak"));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.That((await db.PushInstallations.CountAsync()), Is.EqualTo(0));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/push/installations/unregister", "own", "foreign-object-denial-no-mutation")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/push/installations/disassociate", "own", "foreign-object-denial-no-mutation")]
    public async Task PushInstallationActions_CannotMutateAnotherUsersInstallation()
    {
        var owner = await SeedUserAsync(name: "push-owner", email: "push-owner@example.com", password: "push-pass-123");
        var otherUser = await SeedUserAsync(name: "push-other", email: "push-other@example.com", password: "push-pass-123");
        SetAuthorizationHeader(owner.Id);

        await Client.PostAsJsonAsync("/api/push/installations/register", new
        {
            installationId = "device-owner",
            platform = "ios",
            fcmToken = "token-owner",
            environment = "production"
        });

        using var ownerScope = Factory.Services.CreateScope();
        var ownerDb = ownerScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ownerInstallation = await ownerDb.PushInstallations.SingleAsync(x => x.InstallationId == "device-owner");
        var ownerSessionId = ownerInstallation.SessionId;

        SetAuthorizationHeader(otherUser.Id);
        var unregisterResponse = await Client.PostAsJsonAsync("/api/push/installations/unregister", new { installationId = "device-owner" });
        var disassociateResponse = await Client.PostAsJsonAsync("/api/push/installations/disassociate", new { installationId = "device-owner" });

        Assert.That(unregisterResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(disassociateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        using var assertionScope = Factory.Services.CreateScope();
        var assertionDb = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var installation = await assertionDb.PushInstallations.SingleAsync(x => x.InstallationId == "device-owner");
        Assert.That(installation.UserId, Is.EqualTo(owner.Id));
        Assert.That(installation.SessionId, Is.EqualTo(ownerSessionId));
        Assert.That(installation.DisabledAt, Is.Null);
    }

    [Test]
    public async Task RegisterPushInstallation_CannotTakeOverForeignInstallationOrToken()
    {
        var owner = await SeedUserAsync("push-register-owner", "push-register-owner@example.com");
        var attacker = await SeedUserAsync("push-register-attacker", "push-register-attacker@example.com");
        SetAuthorizationHeader(owner.Id);
        var ownerResponse = await Client.PostAsJsonAsync("/api/push/installations/register", new
        {
            installationId = "device-takeover",
            platform = "android",
            fcmToken = "token-takeover",
            environment = "testing"
        });
        Assert.That(ownerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        SetAuthorizationHeader(attacker.Id);
        var installationTakeover = await Client.PostAsJsonAsync("/api/push/installations/register", new
        {
            installationId = "device-takeover",
            platform = "ios",
            fcmToken = "attacker-token",
            environment = "testing"
        });
        var tokenTakeover = await Client.PostAsJsonAsync("/api/push/installations/register", new
        {
            installationId = "attacker-device",
            platform = "ios",
            fcmToken = "token-takeover",
            environment = "testing"
        });

        Assert.That(installationTakeover.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(tokenTakeover.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var installation = await database.PushInstallations.SingleAsync();
        Assert.That(installation.UserId, Is.EqualTo(owner.Id));
        Assert.That(installation.InstallationId, Is.EqualTo("device-takeover"));
        Assert.That(installation.FcmToken, Is.EqualTo("token-takeover"));
    }

    [Test]
    public async Task RegisterPushInstallation_SameAccountRotatesSession_AndActionsStillRequireExactCurrentPair()
    {
        var owner = await SeedUserAsync("push-session-owner", "push-session-owner@example.com");
        SetAuthorizationHeader(owner.Id);
        var register = await Client.PostAsJsonAsync("/api/push/installations/register", new
        {
            installationId = "device-session-owned",
            platform = "android",
            fcmToken = "token-session-owned",
            environment = "testing"
        });
        Assert.That(register.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        LgymApi.Domain.Entities.UserSession originalSession;
        using (var originalScope = Factory.Services.CreateScope())
        {
            var originalDatabase = originalScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var originalSessionId = (await originalDatabase.PushInstallations.SingleAsync()).SessionId!.Value;
            originalSession = await originalDatabase.UserSessions.AsNoTracking().SingleAsync(session => session.Id == originalSessionId);
        }

        SetAuthorizationHeader(owner.Id);
        var reregister = await Client.PostAsJsonAsync("/api/push/installations/register", new
        {
            installationId = "device-session-owned",
            platform = "android",
            fcmToken = "token-replaced",
            environment = "testing"
        });
        Assert.That(reregister.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var installation = await database.PushInstallations.SingleAsync();
        Assert.That(installation.FcmToken, Is.EqualTo("token-replaced"));
        Assert.That(installation.DisabledAt, Is.Null);
        Assert.That(installation.UserId, Is.EqualTo(owner.Id));
        Assert.That(installation.SessionId, Is.Not.EqualTo(originalSession.Id));

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateJwt(owner.Id, originalSession.Id, originalSession.Jti));
        var oldSessionUnregister = await Client.PostAsJsonAsync(
            "/api/push/installations/unregister",
            new { installationId = "device-session-owned" });
        Assert.That(oldSessionUnregister.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        var currentSession = await database.UserSessions.AsNoTracking()
            .SingleAsync(session => session.Id == installation.SessionId);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateJwt(owner.Id, currentSession.Id, currentSession.Jti));
        var currentSessionUnregister = await Client.PostAsJsonAsync(
            "/api/push/installations/unregister",
            new { installationId = "device-session-owned" });
        Assert.That(currentSessionUnregister.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task RegisterPushInstallation_KeyedRetryReplaysOnce_AndResourceLossDeniesCachedResponse()
    {
        var user = await SeedUserAsync("push-register-replay", "push-register-replay@example.com");
        SetAuthorizationHeader(user.Id);
        SetIdempotencyKey("push-register-replay-key");
        var request = new
        {
            installationId = "device-register-replay",
            platform = "android",
            fcmToken = "token-register-replay",
            environment = "testing"
        };

        var first = await Client.PostAsJsonAsync("/api/push/installations/register", request);
        var replay = await Client.PostAsJsonAsync("/api/push/installations/register", request);
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(replay.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var installation = await database.PushInstallations.SingleAsync();
            installation.UserId = null;
            installation.SessionId = null;
            await database.SaveChangesAsync();
        }

        var deniedReplay = await Client.PostAsJsonAsync("/api/push/installations/register", request);
        ClearIdempotencyKey();
        Assert.That(deniedReplay.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task UnregisterPushInstallation_KeyedRetryReplaysCompletedMutationWithoutUpdatingAgain()
    {
        var user = await SeedUserAsync("push-unregister-replay", "push-unregister-replay@example.com");
        SetAuthorizationHeader(user.Id);
        await Client.PostAsJsonAsync("/api/push/installations/register", new
        {
            installationId = "device-unregister-replay",
            platform = "android",
            fcmToken = "token-unregister-replay",
            environment = "testing"
        });
        SetIdempotencyKey("push-unregister-replay-key");
        var request = new { installationId = "device-unregister-replay" };

        var first = await Client.PostAsJsonAsync("/api/push/installations/unregister", request);
        DateTimeOffset firstUpdatedAt;
        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            firstUpdatedAt = (await database.PushInstallations.SingleAsync()).UpdatedAt;
        }

        var replay = await Client.PostAsJsonAsync("/api/push/installations/unregister", request);
        ClearIdempotencyKey();

        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(replay.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var assertionScope = Factory.Services.CreateScope();
        var assertionDatabase = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.That((await assertionDatabase.PushInstallations.SingleAsync()).UpdatedAt, Is.EqualTo(firstUpdatedAt));
    }

    [TestCase("{", "application/json")]
    [TestCase("{\"installationId\":42}", "application/json")]
    public async Task RegisterPushInstallation_KeyedMalformedRetry_DeniesWithoutThrowing(
        string body,
        string mediaType)
    {
        var identity = Id<User>.New();
        var user = await SeedUserAsync($"push-malformed-{identity}", $"push-malformed-{identity}@example.com");
        SetAuthorizationHeader(user.Id);
        SetIdempotencyKey($"push-malformed-{Id<PushInstallation>.New()}");

        var first = await Client.PostAsync(
            "/api/push/installations/register",
            new StringContent(body, Encoding.UTF8, mediaType));
        var retry = await Client.PostAsync(
            "/api/push/installations/register",
            new StringContent(body, Encoding.UTF8, mediaType));
        ClearIdempotencyKey();

        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(retry.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task RegisterPushInstallation_KeyedRetryAfterSessionRevocation_IsUnauthorized()
    {
        var user = await SeedUserAsync("push-revoked-replay", "push-revoked-replay@example.com");
        SetAuthorizationHeader(user.Id);
        SetIdempotencyKey("push-revoked-replay-key");
        var request = new
        {
            installationId = "device-revoked-replay",
            platform = "android",
            fcmToken = "token-revoked-replay",
            environment = "testing"
        };
        var first = await Client.PostAsJsonAsync("/api/push/installations/register", request);
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var sessionId = (await database.PushInstallations.SingleAsync()).SessionId!.Value;
            (await database.UserSessions.SingleAsync(session => session.Id == sessionId)).RevokedAtUtc = DateTimeOffset.UtcNow;
            await database.SaveChangesAsync();
        }

        var replay = await Client.PostAsJsonAsync("/api/push/installations/register", request);
        ClearIdempotencyKey();
        Assert.That(replay.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task DisassociatePushInstallation_IdempotencyHeaderDoesNotReplayAfterOwnershipIsRemoved()
    {
        var user = await SeedUserAsync("push-disassociate-no-replay", "push-disassociate-no-replay@example.com");
        SetAuthorizationHeader(user.Id);
        await Client.PostAsJsonAsync("/api/push/installations/register", new
        {
            installationId = "device-disassociate-no-replay",
            platform = "android",
            fcmToken = "token-disassociate-no-replay",
            environment = "testing"
        });
        SetIdempotencyKey("push-disassociate-no-replay-key");

        var first = await Client.PostAsJsonAsync(
            "/api/push/installations/disassociate",
            new { installationId = "device-disassociate-no-replay" });
        var retry = await Client.PostAsJsonAsync(
            "/api/push/installations/disassociate",
            new { installationId = "device-disassociate-no-replay" });
        ClearIdempotencyKey();

        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(retry.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task RegisterPushInstallation_AfterUnregister_ReactivatesTheSameInstallation()
    {
        var user = await SeedUserAsync(name: "push-reactivate", email: "push-reactivate@example.com", password: "push-pass-123");
        SetAuthorizationHeader(user.Id);

        await Client.PostAsJsonAsync("/api/push/installations/register", new
        {
            installationId = "device-reactivate",
            platform = "android",
            fcmToken = "token-old",
            environment = "development"
        });
        await Client.PostAsJsonAsync("/api/push/installations/unregister", new { installationId = "device-reactivate" });
        var reregisterResponse = await Client.PostAsJsonAsync("/api/push/installations/register", new
        {
            installationId = "device-reactivate",
            platform = "android",
            fcmToken = "token-new",
            environment = "development"
        });

        await AssertLegacyMessageResponseAsync(reregisterResponse);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var installations = await db.PushInstallations.Where(x => x.InstallationId == "device-reactivate").ToListAsync();
        Assert.That(installations, Has.Exactly(1).Items);
        Assert.That(installations[0].UserId, Is.EqualTo(user.Id));
        Assert.That(installations[0].FcmToken, Is.EqualTo("token-new"));
        Assert.That(installations[0].DisabledAt, Is.Null);
        Assert.That(installations[0].DisabledReason, Is.Null);
    }

    [Test]
    public async Task RegisterPushInstallation_WithAuthenticatedUser_CreatesInstallationRow()
    {
        var user = await SeedUserAsync(name: "push-user", email: "push-user@example.com", password: "push-pass-123");
        SetAuthorizationHeader(user.Id);

        var response = await Client.PostAsJsonAsync("/api/push/installations/register", new
        {
            installationId = "device-1",
            platform = "android",
            fcmToken = "token-1",
            appVersion = "1.0.0",
            environment = "development",
            permissionStatus = "authorized"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var installation = await db.PushInstallations.SingleAsync(x => x.InstallationId == "device-1");

        Assert.That(installation.UserId, Is.EqualTo(user.Id));
        Assert.That(installation.SessionId, Is.Not.Null);
        Assert.That(installation.Platform, Is.EqualTo("android"));
        Assert.That(installation.FcmToken, Is.EqualTo("token-1"));
        Assert.That(installation.AppVersion, Is.EqualTo("1.0.0"));
        Assert.That(installation.Environment, Is.EqualTo("development"));
        Assert.That(installation.PermissionStatus, Is.EqualTo("authorized"));
        Assert.That(installation.DisabledAt, Is.Null);
        Assert.That(installation.DisabledReason, Is.Null);
    }

    [Test]
    public async Task RegisterPushInstallation_WhenRepeatedForSameInstallation_IsIdempotentAndUpdatesExistingRow()
    {
        var user = await SeedUserAsync(name: "push-repeat", email: "push-repeat@example.com", password: "push-pass-123");
        SetAuthorizationHeader(user.Id);

        var firstResponse = await Client.PostAsJsonAsync("/api/push/installations/register", new
        {
            installationId = "device-repeat",
            platform = "ios",
            fcmToken = "token-old",
            appVersion = "1.0.0",
            environment = "production",
            permissionStatus = "provisional"
        });
        Assert.That(firstResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        await Task.Delay(10);

        var secondResponse = await Client.PostAsJsonAsync("/api/push/installations/register", new
        {
            installationId = "device-repeat",
            platform = "ios",
            fcmToken = "token-new",
            appVersion = "1.1.0",
            environment = "production",
            permissionStatus = "authorized"
        });
        Assert.That(secondResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var installations = await db.PushInstallations
            .Where(x => x.InstallationId == "device-repeat")
            .ToListAsync();

        Assert.That(installations, Has.Exactly(1).Items);
        Assert.That(installations[0].FcmToken, Is.EqualTo("token-new"));
        Assert.That(installations[0].AppVersion, Is.EqualTo("1.1.0"));
        Assert.That(installations[0].PermissionStatus, Is.EqualTo("authorized"));
        Assert.That(installations[0].UpdatedAt, Is.GreaterThan(installations[0].CreatedAt));
    }

    [Test]
    public async Task UnregisterPushInstallation_DisablesInstallationWithoutDeletingIt()
    {
        var user = await SeedUserAsync(name: "push-unregister", email: "push-unregister@example.com", password: "push-pass-123");
        SetAuthorizationHeader(user.Id);

        await Client.PostAsJsonAsync("/api/push/installations/register", new
        {
            installationId = "device-unregister",
            platform = "android",
            fcmToken = "token-unregister",
            appVersion = "1.0.0",
            environment = "development"
        });

        var response = await Client.PostAsJsonAsync("/api/push/installations/unregister", new
        {
            installationId = "device-unregister"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var installation = await db.PushInstallations.SingleAsync(x => x.InstallationId == "device-unregister");
        Assert.That(installation.DisabledAt, Is.Not.Null);
        Assert.That(installation.DisabledReason, Is.EqualTo("Unregistered"));
        Assert.That(installation.UserId, Is.EqualTo(user.Id));
    }

    [Test]
    public async Task DisassociatePushInstallation_ClearsUserAndSessionBinding()
    {
        var user = await SeedUserAsync(name: "push-disassociate", email: "push-disassociate@example.com", password: "push-pass-123");
        SetAuthorizationHeader(user.Id);

        await Client.PostAsJsonAsync("/api/push/installations/register", new
        {
            installationId = "device-disassociate",
            platform = "ios",
            fcmToken = "token-disassociate",
            appVersion = "1.0.0",
            environment = "production"
        });

        var response = await Client.PostAsJsonAsync("/api/push/installations/disassociate", new
        {
            installationId = "device-disassociate"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var installation = await db.PushInstallations.SingleAsync(x => x.InstallationId == "device-disassociate");
        Assert.That(installation.UserId, Is.Null);
        Assert.That(installation.SessionId, Is.Null);
        Assert.That(installation.DisabledAt, Is.Null);
    }

    [Test]
    public async Task Logout_DisassociatesInstallationsBoundToCurrentSession()
    {
        await SeedUserAsync(name: "push-logout", email: "push-logout@example.com", password: "push-pass-123");
        var login = await LoginAsync("push-logout", "push-pass-123");
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);

        await Client.PostAsJsonAsync("/api/push/installations/register", new
        {
            installationId = "device-logout",
            platform = "android",
            fcmToken = "token-logout",
            appVersion = "1.0.0",
            environment = "production"
        });

        var logoutResponse = await Client.PostAsync("/api/logout", null);
        Assert.That(logoutResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var installation = await db.PushInstallations.SingleAsync(x => x.InstallationId == "device-logout");
        Assert.That(installation.UserId, Is.Null);
        Assert.That(installation.SessionId, Is.Null);
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/push/installations/unregister", "own", "anonymous-denial")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/push/installations/register", "own", "anonymous-denial")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/push/installations/disassociate", "own", "anonymous-denial")]
    public async Task RegisterPushInstallation_WithoutAuthorization_IsRejectedAndCreatesNoRows()
    {
        var response = await Client.PostAsJsonAsync("/api/push/installations/register", new
        {
            installationId = "device-anon",
            platform = "android",
            fcmToken = "token-anon",
            appVersion = "1.0.0",
            environment = "development"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        using (var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            Assert.That(body.RootElement.GetProperty("msg").GetString(), Is.EqualTo(CompatibilityResourceMessage.InCulture("en", () => Messages.InvalidToken)));
            Assert.That(body.RootElement.TryGetProperty("message", out _), Is.False);
        }

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.That((await db.PushInstallations.CountAsync()), Is.EqualTo(0));
    }

    private async Task<LoginResponse> LoginAsync(string name, string password)
    {
        var response = await Client.PostAsJsonAsync("/api/login", new { name, password });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(body, Is.Not.Null);
        return body!;
    }

    private static async Task AssertLegacyMessageResponseAsync(HttpResponseMessage response)
    {
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(document.RootElement.TryGetProperty("msg", out var message), Is.True);
        Assert.That(message.ValueKind, Is.EqualTo(JsonValueKind.String));
        Assert.That(document.RootElement.TryGetProperty("message", out _), Is.False);
    }

    private sealed class LoginResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;
    }
}
