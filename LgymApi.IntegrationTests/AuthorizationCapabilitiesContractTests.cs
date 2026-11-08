using System.Net;
using System.Text.Json;
using FluentAssertions;
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

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.Private.Should().BeTrue();
        response.Headers.CacheControl.NoStore.Should().BeTrue();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.EnumerateObject().Select(property => property.Name)
            .Should().Equal("schemaVersion", "account", "application", "extensions");
        payload.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        payload.RootElement.GetProperty("account").GetProperty("_id").GetString().Should().Be(administrator.Id.ToString());

        var application = payload.RootElement.GetProperty("application");
        application.GetProperty("canAccessAdminWorkspace").GetBoolean().Should().BeTrue();
        application.GetProperty("canManageUserRoles").GetBoolean().Should().BeTrue();
        application.GetProperty("canManageAppConfig").GetBoolean().Should().BeTrue();
        application.GetProperty("canManageGlobalExercises").GetBoolean().Should().BeTrue();
        application.GetProperty("canAccessTrainerWorkspace").GetBoolean().Should().BeFalse();

        var extensions = payload.RootElement.GetProperty("extensions");
        extensions.EnumerateObject().Select(property => property.Name)
            .Should().Equal("reporting", "trainingPlanning", "coaching", "nutrition", "workoutProgress", "notifications");
        extensions.EnumerateObject().Where(property => property.Name is not "notifications" and not "nutrition").Should().OnlyContain(property =>
            property.Value.EnumerateObject().Select(nested => nested.Name).SequenceEqual(new[] { "available" })
            && !property.Value.GetProperty("available").GetBoolean());
        var nutrition = extensions.GetProperty("nutrition");
        nutrition.GetProperty("available").GetBoolean().Should().BeTrue();
        nutrition.GetProperty("canReadOwnDietPlans").GetBoolean().Should().BeTrue();
        nutrition.GetProperty("canUpdateOwnDietPlans").GetBoolean().Should().BeTrue();
        nutrition.GetProperty("canCreateDietPlans").GetBoolean().Should().BeFalse();
        nutrition.GetProperty("canManageSupplementation").GetBoolean().Should().BeFalse();
        var notifications = extensions.GetProperty("notifications");
        notifications.GetProperty("available").GetBoolean().Should().BeTrue();
        notifications.GetProperty("canReadNotifications").GetBoolean().Should().BeTrue();
        notifications.GetProperty("canMarkNotificationsRead").GetBoolean().Should().BeTrue();
        notifications.GetProperty("canManagePushInstallations").GetBoolean().Should().BeTrue();
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

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var application = payload.RootElement.GetProperty("application");
        application.EnumerateObject().Should().OnlyContain(property => !property.Value.GetBoolean());
    }

    [Test]
    public async Task GetAccess_IsSelfOnlyAndDoesNotAcceptAForeignAccountTarget()
    {
        var actor = await SeedUserAsync("access-actor", "access-actor@example.com");
        var foreignAccount = await SeedUserAsync("access-foreign", "access-foreign@example.com");
        SetAuthorizationHeader(actor.Id);

        using var queryResponse = await Client.GetAsync($"/api/account/access?accountId={foreignAccount.Id}");
        using var foreignRouteResponse = await Client.GetAsync($"/api/account/{foreignAccount.Id}/access");

        queryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var payload = JsonDocument.Parse(await queryResponse.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("account").GetProperty("_id").GetString().Should().Be(actor.Id.ToString());
        foreignRouteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/account/access", "own", "anonymous-denial")]
    public async Task GetAccess_WithoutAuthentication_Returns401()
    {
        using var response = await Client.GetAsync("/api/account/access");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("msg").GetString().Should().NotBeNullOrWhiteSpace();
        payload.RootElement.TryGetProperty("req", out _).Should().BeFalse();
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

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("msg").GetString().Should().NotBeNullOrWhiteSpace();
        payload.RootElement.TryGetProperty("req", out _).Should().BeFalse();
        payload.RootElement.TryGetProperty("application", out _).Should().BeFalse();
        payload.RootElement.TryGetProperty("extensions", out _).Should().BeFalse();
    }

    [Test]
    public async Task OpenApi_PublishesSelfOnlyTypedEnvelopeAndCanExportExactTestingSnapshot()
    {
        var document = Factory.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        var json = await document.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_0);
        using var payload = JsonDocument.Parse(json);
        var operation = payload.RootElement.GetProperty("paths").GetProperty("/api/account/access").GetProperty("get");
        operation.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()).Should().Contain("Authorization");
        operation.TryGetProperty("parameters", out _).Should().BeFalse();
        foreach (var status in new[] { "401", "403" })
        {
            operation.GetProperty("responses").GetProperty(status).GetProperty("content")
                .GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString()
                .Should().Be("#/components/schemas/ResponseMessageDto");
        }
        operation.GetProperty("responses").GetProperty("200").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString()
            .Should().Be("#/components/schemas/AccountAccessResponseDto");
        var schemas = payload.RootElement.GetProperty("components").GetProperty("schemas");
        schemas.GetProperty("AccountAccessResponseDto").GetProperty("properties").EnumerateObject()
            .Select(property => property.Name).Should().BeEquivalentTo("schemaVersion", "account", "application", "extensions");
        schemas.GetProperty("AccountAccessOwnerCapabilitiesDto").GetProperty("properties").GetProperty("available")
            .GetProperty("type").GetString().Should().Be("boolean");
        schemas.GetProperty("NotificationsAccountAccessOwnerCapabilitiesDto").GetProperty("properties")
            .EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo("available", "canReadNotifications", "canMarkNotificationsRead", "canManagePushInstallations");

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

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var application = payload.RootElement.GetProperty("application");
        application.GetProperty("canAccessTrainerWorkspace").GetBoolean().Should().BeTrue();
        application.GetProperty("canAccessAdminWorkspace").GetBoolean().Should().Be(isAdmin);
        var extensions = payload.RootElement.GetProperty("extensions");
        extensions.EnumerateObject().Where(property => property.Name is not "notifications" and not "nutrition")
            .Should().OnlyContain(property => !property.Value.GetProperty("available").GetBoolean());
        extensions.GetProperty("nutrition").GetProperty("canCreateDietPlans").GetBoolean().Should().BeTrue();
        extensions.GetProperty("nutrition").GetProperty("canManageSupplementation").GetBoolean().Should().BeTrue();
        extensions.GetProperty("notifications").GetProperty("canManagePushInstallations").GetBoolean().Should().BeTrue();
    }
}
