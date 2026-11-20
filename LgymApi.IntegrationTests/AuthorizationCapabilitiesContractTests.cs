using System.Net;
using System.Text.Json;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Infrastructure.Data;
using LgymApi.TestUtils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class AuthorizationCapabilitiesContractTests : IntegrationTestBase
{
    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/account/access", "own", "owner-allow")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/account/access", "own", "no-client-subject")]
    public async Task GetAccess_ReturnsCurrentIdentityPolicyCapabilitiesAndFailClosedOwnerExtensions()
    {
        var administrator = await SeedUserAsync("access-admin", "access-admin@example.com", isAdmin: true);
        SetAuthorizationHeader(administrator.Id);

        using var response = await Client.GetAsync("/api/account/access");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Headers.CacheControl!.Private, Is.True);
        Assert.That(response.Headers.CacheControl.NoStore, Is.True);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(payload.RootElement.EnumerateObject().Select(property => property.Name), Is.EqualTo(new[] { "schemaVersion", "account", "application", "extensions" }));
        Assert.That(payload.RootElement.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(1));
        Assert.That(payload.RootElement.GetProperty("account").GetProperty("_id").GetString(), Is.EqualTo(administrator.Id.ToString()));

        var application = payload.RootElement.GetProperty("application");
        Assert.That(application.GetProperty("canAccessAdminWorkspace").GetBoolean(), Is.True);
        Assert.That(application.GetProperty("canManageUserRoles").GetBoolean(), Is.True);
        Assert.That(application.GetProperty("canManageAppConfig").GetBoolean(), Is.True);
        Assert.That(application.GetProperty("canManageGlobalExercises").GetBoolean(), Is.True);
        Assert.That(application.GetProperty("canAccessTrainerWorkspace").GetBoolean(), Is.False);

        var extensions = payload.RootElement.GetProperty("extensions");
        Assert.That(extensions.EnumerateObject().Select(property => property.Name), Is.EqualTo(new[] { "reporting", "trainingPlanning", "coaching", "nutrition", "workoutProgress", "notifications" }));
        Assert.That((extensions.EnumerateObject().Where(property => property.Name is not "reporting" and not "notifications" and not "nutrition")).All(property =>
            property.Value.EnumerateObject().Select(nested => nested.Name).SequenceEqual(new[] { "available" })
            && !property.Value.GetProperty("available").GetBoolean()), Is.True);
        var reporting = extensions.GetProperty("reporting");
        Assert.That(reporting.GetProperty("available").GetBoolean(), Is.True);
        Assert.That(reporting.GetProperty("canReadOwnSubmissions").GetBoolean(), Is.True);
        Assert.That(reporting.GetProperty("canSubmitReportRequests").GetBoolean(), Is.True);
        Assert.That(reporting.GetProperty("canManageReportTemplates").GetBoolean(), Is.False);
        Assert.That(reporting.GetProperty("canManagePhotoTypes").GetBoolean(), Is.False);
        var nutrition = extensions.GetProperty("nutrition");
        Assert.That(nutrition.GetProperty("available").GetBoolean(), Is.True);
        Assert.That(nutrition.GetProperty("canReadOwnDietPlans").GetBoolean(), Is.True);
        Assert.That(nutrition.GetProperty("canUpdateOwnDietPlans").GetBoolean(), Is.True);
        Assert.That(nutrition.GetProperty("canCreateDietPlans").GetBoolean(), Is.False);
        Assert.That(nutrition.GetProperty("canManageSupplementation").GetBoolean(), Is.False);
        var notifications = extensions.GetProperty("notifications");
        Assert.That(notifications.GetProperty("available").GetBoolean(), Is.True);
        Assert.That(notifications.GetProperty("canReadNotifications").GetBoolean(), Is.True);
        Assert.That(notifications.GetProperty("canMarkNotificationsRead").GetBoolean(), Is.True);
        Assert.That(notifications.GetProperty("canManagePushInstallations").GetBoolean(), Is.True);
    }

    [Test]
    public async Task GetAccess_UsesCurrentPersistedPermissionsInsteadOfTheIssuedToken()
    {
        var administrator = await SeedUserAsync("access-demoted", "access-demoted@example.com", isAdmin: true);
        SetAuthorizationHeader(administrator.Id);

        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var administratorRoles = await database.UserRoles
                .Where(userRole => userRole.UserId == administrator.Id && userRole.Role.Name == AuthConstants.Roles.Admin)
                .ToListAsync();
            database.UserRoles.RemoveRange(administratorRoles);
            await database.SaveChangesAsync();
        }

        using var response = await Client.GetAsync("/api/account/access");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var application = payload.RootElement.GetProperty("application");
        Assert.That((application.EnumerateObject()).All(property => !property.Value.GetBoolean()), Is.True);
    }

    [Test]
    public async Task GetAccess_IsSelfOnlyAndDoesNotAcceptAForeignAccountTarget()
    {
        var actor = await SeedUserAsync("access-actor", "access-actor@example.com");
        var foreignAccount = await SeedUserAsync("access-foreign", "access-foreign@example.com");
        SetAuthorizationHeader(actor.Id);

        using var queryResponse = await Client.GetAsync($"/api/account/access?accountId={foreignAccount.Id}");
        using var foreignRouteResponse = await Client.GetAsync($"/api/account/{foreignAccount.Id}/access");

        Assert.That(queryResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var payload = JsonDocument.Parse(await queryResponse.Content.ReadAsStringAsync());
        Assert.That(payload.RootElement.GetProperty("account").GetProperty("_id").GetString(), Is.EqualTo(actor.Id.ToString()));
        Assert.That(foreignRouteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/account/access", "own", "anonymous-denial")]
    public async Task GetAccess_WithoutAuthentication_Returns401()
    {
        using var response = await Client.GetAsync("/api/account/access");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(string.IsNullOrWhiteSpace(payload.RootElement.GetProperty("msg").GetString()), Is.False);
        Assert.That(payload.RootElement.TryGetProperty("req", out _), Is.False);
    }

    [Test]
    public async Task GetAccess_BlockedAccount_Returns403WithoutCapabilities()
    {
        var account = await SeedUserAsync("access-blocked", "access-blocked@example.com", isAdmin: true);
        SetAuthorizationHeader(account.Id);
        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var persistedAccount = await database.Users.SingleAsync(user => user.Id == account.Id);
            persistedAccount.IsBlocked = true;
            await database.SaveChangesAsync();
        }

        using var response = await Client.GetAsync("/api/account/access");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(string.IsNullOrWhiteSpace(payload.RootElement.GetProperty("msg").GetString()), Is.False);
        Assert.That(payload.RootElement.TryGetProperty("req", out _), Is.False);
        Assert.That(payload.RootElement.TryGetProperty("application", out _), Is.False);
        Assert.That(payload.RootElement.TryGetProperty("extensions", out _), Is.False);
    }

    [Test]
    public async Task OpenApi_PublishesSelfOnlyTypedEnvelopeAndCanExportExactTestingSnapshot()
    {
        var document = Factory.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        var json = await document.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_0);
        using var payload = JsonDocument.Parse(json);
        var operation = payload.RootElement.GetProperty("paths").GetProperty("/api/account/access").GetProperty("get");
        Assert.That(operation.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()), Does.Contain("Authorization"));
        Assert.That(operation.TryGetProperty("parameters", out _), Is.False);
        foreach (var status in new[] { "401", "403" })
        {
            Assert.That(operation.GetProperty("responses").GetProperty(status).GetProperty("content")
                .GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString(), Is.EqualTo("#/components/schemas/ResponseMessageDto"));
        }
        Assert.That(operation.GetProperty("responses").GetProperty("200").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString(), Is.EqualTo("#/components/schemas/AccountAccessResponseDto"));
        var schemas = payload.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.That(schemas.GetProperty("AccountAccessResponseDto").GetProperty("properties").EnumerateObject()
            .Select(property => property.Name), Is.EquivalentTo(new[] { "schemaVersion", "account", "application", "extensions" }));
        Assert.That(schemas.GetProperty("AccountAccessOwnerCapabilitiesDto").GetProperty("properties").GetProperty("available")
            .GetProperty("type").GetString(), Is.EqualTo("boolean"));
        Assert.That(schemas.GetProperty("ReportingAccountAccessOwnerCapabilitiesDto").GetProperty("properties")
            .EnumerateObject().Select(property => property.Name), Is.EquivalentTo(new[] { "available", "canReadOwnSubmissions", "canSubmitReportRequests", "canManageReportTemplates", "canManagePhotoTypes" }));
        Assert.That(schemas.GetProperty("NotificationsAccountAccessOwnerCapabilitiesDto").GetProperty("properties")
            .EnumerateObject().Select(property => property.Name), Is.EquivalentTo(new[] { "available", "canReadNotifications", "canMarkNotificationsRead", "canManagePushInstallations" }));

        var exportPath = Environment.GetEnvironmentVariable("LGYM_CAPABILITIES_OPENAPI_OUTPUT");
        if (!string.IsNullOrWhiteSpace(exportPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(exportPath))!);
            await File.WriteAllTextAsync(exportPath, json);
            TestContext.AddTestAttachment(exportPath, "Exact Swagger from the local Testing host; production Swagger exposure unchanged.");
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task GetAccess_TrainerAndMixedRoleAccounts_PreserveIndependentCapabilities(bool isAdmin)
    {
        var account = await SeedUserAsync("access-trainer", "trainer@example.com", isAdmin: isAdmin);
        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.UserRoles.Add(TestDataFactory.ApprovedTrainerRole(database, account.Id));
            await database.SaveChangesAsync();
        }
        SetAuthorizationHeader(account.Id);

        using var response = await Client.GetAsync("/api/account/access");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var application = payload.RootElement.GetProperty("application");
        Assert.That(application.GetProperty("canAccessTrainerWorkspace").GetBoolean(), Is.True);
        Assert.That(application.GetProperty("canAccessAdminWorkspace").GetBoolean(), Is.EqualTo(isAdmin));
        var extensions = payload.RootElement.GetProperty("extensions");
        Assert.That((extensions.EnumerateObject().Where(property => property.Name is not "reporting" and not "notifications" and not "nutrition")).All(property => !property.Value.GetProperty("available").GetBoolean()), Is.True);
        var reporting = extensions.GetProperty("reporting");
        Assert.That(reporting.GetProperty("available").GetBoolean(), Is.True);
        Assert.That(reporting.GetProperty("canReadOwnSubmissions").GetBoolean(), Is.True);
        Assert.That(reporting.GetProperty("canSubmitReportRequests").GetBoolean(), Is.True);
        Assert.That(reporting.GetProperty("canManageReportTemplates").GetBoolean(), Is.True);
        Assert.That(reporting.GetProperty("canManagePhotoTypes").GetBoolean(), Is.True);
        Assert.That(extensions.GetProperty("nutrition").GetProperty("canCreateDietPlans").GetBoolean(), Is.True);
        Assert.That(extensions.GetProperty("nutrition").GetProperty("canManageSupplementation").GetBoolean(), Is.True);
        Assert.That(extensions.GetProperty("notifications").GetProperty("canManagePushInstallations").GetBoolean(), Is.True);
    }
}
