using LgymApi.IntegrationTests.Authorization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Data.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class TrainerApprovalTests : IntegrationTestBase
{
    [TestCase("approve", "Approved")]
    [TestCase("reject", "Rejected")]
    [AuthorizationEvidence("GET", "/api/admin/users/trainer-applications", "admin", "current-permission-allow")]
    [AuthorizationEvidence("GET", "/api/admin/users/{id}/trainer-application", "admin", "current-permission-allow")]
    [AuthorizationEvidence("POST", "/api/admin/users/{id}/trainer-application/approve", "admin", "current-permission-allow")]
    [AuthorizationEvidence("POST", "/api/admin/users/{id}/trainer-application/reject", "admin", "current-permission-allow")]
    public async Task Decision_PersistsAudit_IsIdempotent_AndRejectsConflictingDecision(string action, string expected)
    {
        var applicant = await RegisterAsync();
        var admin = await SeedAdminAsync();
        SetAuthorizationHeader(admin.Id);
        var list = await Client.GetAsync("/api/admin/users/trainer-applications?page=1&pageSize=1");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var listing = await Body(list);
        listing.GetProperty("totalCount").GetInt32().Should().Be(1);
        listing.GetProperty("items")[0].GetProperty("accountId").GetString().Should().Be(applicant.Id.ToString());

        var first = await Client.PostAsync($"/api/admin/users/{applicant.Id}/trainer-application/{action}", null);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var decision = await Body(first);
        decision.GetProperty("status").GetString().Should().Be(expected);
        decision.GetProperty("reviewedBy").GetString().Should().Be(admin.Id.ToString());
        decision.GetProperty("decidedAt").GetDateTimeOffset().Offset.Should().Be(TimeSpan.Zero);
        var repeated = await Client.PostAsync($"/api/admin/users/{applicant.Id}/trainer-application/{action}", null);
        repeated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Body(repeated)).GetProperty("decidedAt").GetString().Should().Be(decision.GetProperty("decidedAt").GetString());
        var conflicting = await Client.PostAsync($"/api/admin/users/{applicant.Id}/trainer-application/{(action == "approve" ? "reject" : "approve")}", null);
        conflicting.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Body(conflicting)).GetProperty("code").GetString().Should().Be("TrainerApprovalConflict");
        var detail = await Client.GetAsync($"/api/admin/users/{applicant.Id}/trainer-application");
        (await Body(detail)).GetProperty("status").GetString().Should().Be(expected);
        (await Body(await Client.GetAsync("/api/admin/users/trainer-applications"))).GetProperty("totalCount").GetInt32().Should().Be(0);

        ClearAuthorizationHeader();
        var login = await Client.PostAsJsonAsync("/api/trainer/login", new { name = applicant.Name, password = "password123" });
        login.StatusCode.Should().Be(action == "approve" ? HttpStatusCode.OK : HttpStatusCode.Forbidden);
        if (action == "approve")
        {
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await Body(login)).GetProperty("token").GetString());
            (await Client.GetAsync("/api/trainer/checkToken")).StatusCode.Should().Be(HttpStatusCode.OK);
        }
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.UserRoles.AnyAsync(role => role.UserId == applicant.Id && role.Role.Name == AuthConstants.Roles.Trainer))
            .Should().Be(action == "approve");
    }

    [TestCase("en")]
    [TestCase("pl")]
    public async Task PendingLogin_OnlyRevealsStateAfterValidPassword_AndPreservesAthleteLogin(string language)
    {
        var applicant = await RegisterAsync();
        Client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        var wrong = await Client.PostAsJsonAsync("/api/trainer/login", new { name = applicant.Name, password = "wrong-password" });
        wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Body(wrong)).TryGetProperty("code", out _).Should().BeFalse();
        var correct = await Client.PostAsJsonAsync("/api/trainer/login", new { name = applicant.Name, password = "password123" });
        correct.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var denied = await Body(correct);
        denied.GetProperty("code").GetString().Should().Be("TrainerPendingApproval");
        denied.GetProperty("msg").GetString().Should().Contain(language == "pl" ? "zatwierdzenie" : "approval");
        using (var scope = Factory.Services.CreateScope())
            (await scope.ServiceProvider.GetRequiredService<AppDbContext>().UserSessions.CountAsync()).Should().Be(0);
        var athlete = await Client.PostAsJsonAsync("/api/login", new { name = applicant.Name, password = "password123" });
        athlete.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = await Body(athlete);
        data.GetProperty("req").GetProperty("trainerApprovalStatus").GetString().Should().Be("PendingApproval");
        data.GetProperty("permissionClaims").EnumerateArray().Select(value => value.GetString()).Should().NotContain("trainer.access");
    }

    [TestCase(TrainerApprovalStatus.PendingApproval)]
    [TestCase(TrainerApprovalStatus.Rejected)]
    public async Task OldToken_AndInjectedTrainerRole_CannotUseAnyTrainerPolicyRoute(TrainerApprovalStatus status)
    {
        var applicant = await RegisterAsync();
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(user => user.Id == applicant.Id);
            user.TrainerApprovalStatus = status;
            db.UserRoles.Add(new UserRole { UserId = applicant.Id, RoleId = RoleSeedDataConfiguration.TrainerRoleSeedId });
            await db.SaveChangesAsync();
        }
        // Generate a real old-format JWT containing Trainer and trainer.access.
        SetAuthorizationHeader(applicant.Id);
        var endpoints = Factory.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>().Endpoints
            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>()
                .Any(auth => auth.Policy == AuthConstants.Policies.TrainerAccess));
        var tested = 0;
        foreach (var endpoint in endpoints)
        {
            var path = endpoint.RoutePattern.RawText!;
            foreach (var parameter in endpoint.RoutePattern.Parameters)
                path = System.Text.RegularExpressions.Regex.Replace(path, "\\{" + parameter.Name + "[^}]*\\}", applicant.Id.ToString());
            var methods = endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()!.HttpMethods;
            foreach (var method in methods)
            {
                using var request = new HttpRequestMessage(new HttpMethod(method), "/" + path.TrimStart('/'));
                request.Content = JsonContent.Create(new { });
                var response = await Client.SendAsync(request);
                response.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{method} {path}");
                tested++;
            }
        }
        tested.Should().BeGreaterThan(20);
        (await Client.GetAsync("/api/checkToken")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [TestCase("approve")]
    [TestCase("reject")]
    public async Task Review_DeniesAnonymousAthleteSelfRevokedAdminAndInvalidApplicants(string action)
    {
        var applicant = await RegisterAsync();
        var url = $"/api/admin/users/{applicant.Id}/trainer-application/{action}";
        (await Client.PostAsync(url, null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        SetAuthorizationHeader(applicant.Id);
        (await Client.PostAsync(url, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var admin = await SeedAdminAsync();
        SetAuthorizationHeader(admin.Id);
        (await Client.PostAsync($"/api/admin/users/{admin.Id}/trainer-application/{action}", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Client.PostAsync($"/api/admin/users/not-an-id/trainer-application/{action}", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.PostAsync($"/api/admin/users/{LgymApi.Domain.ValueObjects.Id<User>.New()}/trainer-application/{action}", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var ordinary = await SeedUserAsync("ordinary", "ordinary@example.test");
        (await Client.PostAsync($"/api/admin/users/{ordinary.Id}/trainer-application/{action}", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserRoles.RemoveRange(await db.UserRoles.Where(role => role.UserId == admin.Id && role.Role.Name == "Admin").ToListAsync());
            await db.SaveChangesAsync();
        }
        (await Client.PostAsync(url, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Client.GetAsync("/api/admin/users/trainer-applications")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task GenericRoles_CannotGrantTrainerApproval_OrSmuggleTrainerPermission()
    {
        var applicant = await RegisterAsync();
        var admin = await SeedAdminAsync();
        SetAuthorizationHeader(admin.Id);
        var direct = await Client.PostAsJsonAsync($"/api/roles/users/{applicant.Id}/roles", new { roles = new[] { "User", "Trainer" } });
        direct.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Body(direct)).GetProperty("code").GetString().Should().Be("TrainerApprovalRequired");
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userRole = await db.Roles.Include(role => role.RoleClaims).SingleAsync(role => role.Name == "User");
            userRole.RoleClaims.Add(new RoleClaim { Id = LgymApi.Domain.ValueObjects.Id<RoleClaim>.New(),
                RoleId = userRole.Id, ClaimType = AuthConstants.PermissionClaimType, ClaimValue = AuthConstants.Permissions.TrainerAccess });
            await db.SaveChangesAsync();
        }
        SetAuthorizationHeader(applicant.Id);
        (await Client.GetAsync("/api/trainer/checkToken")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var profile = await Body(await Client.GetAsync("/api/checkToken"));
        profile.GetProperty("permissionClaims").EnumerateArray().Select(value => value.GetString()).Should().NotContain("trainer.access");
    }

    [Test]
    public async Task RepeatedRegistration_ReplaysPendingConfirmationWithoutDuplicatingAccountOrSession()
    {
        var applicant = await RegisterAsync();
        var repeated = await RegisterAsync();
        repeated.Id.Should().Be(applicant.Id);
        repeated.TrainerApplicationCreatedAt.Should().Be(applicant.TrainerApplicationCreatedAt);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Users.CountAsync()).Should().Be(1);
        (await db.UserSessions.CountAsync()).Should().Be(0);
        (await db.UserRoles.AnyAsync(role => role.Role.Name == "Trainer")).Should().BeFalse();
    }

    private async Task<User> RegisterAsync()
    {
        SetIdempotencyKey("trainer-approval-registration");
        var response = await Client.PostAsJsonAsync("/api/trainer/register", new
        { name = "applicant", email = "applicant@example.test", password = "password123", cpassword = "password123" });
        ClearIdempotencyKey();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AsNoTracking().SingleAsync(user => user.Name == "applicant");
    }

    private static async Task<JsonElement> Body(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
}
