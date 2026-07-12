using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class PushInstallationApiTests : IntegrationTestBase
{
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

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var installation = await db.PushInstallations.SingleAsync(x => x.InstallationId == "device-1");

        installation.UserId.Should().Be(user.Id);
        installation.SessionId.Should().NotBeNull();
        installation.Platform.Should().Be("android");
        installation.FcmToken.Should().Be("token-1");
        installation.AppVersion.Should().Be("1.0.0");
        installation.Environment.Should().Be("development");
        installation.PermissionStatus.Should().Be("authorized");
        installation.DisabledAt.Should().BeNull();
        installation.DisabledReason.Should().BeNull();
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
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

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
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var installations = await db.PushInstallations
            .Where(x => x.InstallationId == "device-repeat")
            .ToListAsync();

        installations.Should().HaveCount(1);
        installations[0].FcmToken.Should().Be("token-new");
        installations[0].AppVersion.Should().Be("1.1.0");
        installations[0].PermissionStatus.Should().Be("authorized");
        installations[0].UpdatedAt.Should().BeAfter(installations[0].CreatedAt);
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

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var installation = await db.PushInstallations.SingleAsync(x => x.InstallationId == "device-unregister");
        installation.DisabledAt.Should().NotBeNull();
        installation.DisabledReason.Should().Be("Unregistered");
        installation.UserId.Should().Be(user.Id);
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

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var installation = await db.PushInstallations.SingleAsync(x => x.InstallationId == "device-disassociate");
        installation.UserId.Should().BeNull();
        installation.SessionId.Should().BeNull();
        installation.DisabledAt.Should().BeNull();
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
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var installation = await db.PushInstallations.SingleAsync(x => x.InstallationId == "device-logout");
        installation.UserId.Should().BeNull();
        installation.SessionId.Should().BeNull();
    }

    [Test]
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

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.PushInstallations.CountAsync()).Should().Be(0);
    }

    private async Task<LoginResponse> LoginAsync(string name, string password)
    {
        var response = await Client.PostAsJsonAsync("/api/login", new { name, password });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        body.Should().NotBeNull();
        return body!;
    }

    private sealed class LoginResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;
    }
}
