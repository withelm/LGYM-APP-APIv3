using LgymApi.IntegrationTests.Authorization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
    private static readonly string[] UserAndTrainerRoles = [AuthConstants.Roles.User, AuthConstants.Roles.Trainer];

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/application/resubmit", "own", "owner-allow")]
    [AuthorizationEvidence("POST", "/api/trainer/application/resubmit", "own", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/application/resubmit", "own", "no-client-subject")]
    public async Task RejectedApplicant_CanResubmit_WithoutGrantingAccessOrLosingRejection()
    {
        const string resubmit = "/api/trainer/application/resubmit";
        var applicant = await RegisterAsync();
        var admin = await SeedAdminAsync();
        var decision = $"/api/admin/users/{applicant.Id}/trainer-application";
        Assert.That((await Client.PostAsync(resubmit, null)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        SetAuthorizationHeader(applicant.Id);
        Assert.That((await Client.PostAsync(resubmit, null)).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        SetAuthorizationHeader(admin.Id);
        var rejection = await Body(await Client.PostAsync(decision + "/reject", null));
        Assert.That((await Client.PostAsJsonAsync(resubmit, new { accountId = applicant.Id.ToString() }))
            .StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

        ClearAuthorizationHeader();
        var login = await Client.PostAsJsonAsync("/api/login", new { name = applicant.Name, password = "password123" });
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await Body(login)).GetProperty("token").GetString());
        Assert.That((await Client.PostAsync(resubmit, null)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await Client.PostAsync(resubmit, null)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await Client.GetAsync("/api/trainer/checkToken")).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var attempts = await db.TrainerApplications.Where(item => item.ApplicantId == applicant.Id)
                .OrderBy(item => item.Attempt).ToListAsync();
            Assert.That(attempts, Has.Exactly(2).Items);
            Assert.That(attempts[0].Status, Is.EqualTo(TrainerApprovalStatus.Rejected));
            Assert.That(attempts[0].CreatedAt, Is.EqualTo(rejection.GetProperty("createdAt").GetDateTimeOffset()));
            Assert.That(attempts[0].ReviewedBy, Is.EqualTo(admin.Id));
            Assert.That(attempts[0].DecidedAt, Is.EqualTo(rejection.GetProperty("decidedAt").GetDateTimeOffset()));
            Assert.That(attempts[1].Status, Is.EqualTo(TrainerApprovalStatus.PendingApproval));
            Assert.That(attempts[1].ReviewedBy, Is.Null);
            Assert.That(attempts[1].DecidedAt, Is.Null);
        }

        SetAuthorizationHeader(admin.Id);
        var queue = await Body(await Client.GetAsync("/api/admin/users/trainer-applications"));
        Assert.That(queue.GetProperty("totalCount").GetInt32(), Is.EqualTo(1));
        Assert.That(queue.GetProperty("items")[0].GetProperty("attempt").GetInt32(), Is.EqualTo(2));
        var history = await Body(await Client.GetAsync("/api/admin/users/trainer-applications?includeReviewed=true"));
        Assert.That(history.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("status").GetString()), Is.EquivalentTo(new[] { "Rejected", "PendingApproval" }));
        Assert.That((await Client.PostAsync(decision + "/approve?attempt=1", null)).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await Client.PostAsync(decision + "/reject", null)).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await Client.PostAsync(decision + "/approve?attempt=2", null)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        SetAuthorizationHeader(applicant.Id);
        Assert.That((await Client.GetAsync("/api/trainer/checkToken")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await Client.PostAsync(resubmit, null)).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

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
        Assert.That(list.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var listing = await Body(list);
        Assert.That(listing.GetProperty("totalCount").GetInt32(), Is.EqualTo(1));
        Assert.That(listing.GetProperty("items")[0].GetProperty("accountId").GetString(), Is.EqualTo(applicant.Id.ToString()));

        var first = await Client.PostAsync($"/api/admin/users/{applicant.Id}/trainer-application/{action}", null);
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var decision = await Body(first);
        Assert.That(decision.GetProperty("status").GetString(), Is.EqualTo(expected));
        Assert.That(decision.GetProperty("reviewedBy").GetString(), Is.EqualTo(admin.Id.ToString()));
        Assert.That(decision.GetProperty("decidedAt").GetDateTimeOffset().Offset, Is.EqualTo(TimeSpan.Zero));
        var repeated = await Client.PostAsync($"/api/admin/users/{applicant.Id}/trainer-application/{action}", null);
        Assert.That(repeated.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await Body(repeated)).GetProperty("decidedAt").GetString(), Is.EqualTo(decision.GetProperty("decidedAt").GetString()));
        var conflicting = await Client.PostAsync($"/api/admin/users/{applicant.Id}/trainer-application/{(action == "approve" ? "reject" : "approve")}", null);
        Assert.That(conflicting.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await Body(conflicting)).GetProperty("code").GetString(), Is.EqualTo("TrainerApprovalConflict"));
        var detail = await Client.GetAsync($"/api/admin/users/{applicant.Id}/trainer-application");
        Assert.That((await Body(detail)).GetProperty("status").GetString(), Is.EqualTo(expected));
        Assert.That((await Body(await Client.GetAsync("/api/admin/users/trainer-applications"))).GetProperty("totalCount").GetInt32(), Is.EqualTo(0));

        ClearAuthorizationHeader();
        var login = await Client.PostAsJsonAsync("/api/trainer/login", new { name = applicant.Name, password = "password123" });
        Assert.That(login.StatusCode, Is.EqualTo(action == "approve" ? HttpStatusCode.OK : HttpStatusCode.Forbidden));
        if (action == "approve")
        {
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await Body(login)).GetProperty("token").GetString());
            Assert.That((await Client.GetAsync("/api/trainer/checkToken")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.That((await db.UserRoles.AnyAsync(role => role.UserId == applicant.Id && role.Role.Name == AuthConstants.Roles.Trainer)), Is.EqualTo(action == "approve"));
    }

    [TestCase("en")]
    [TestCase("pl")]
    public async Task PendingLogin_OnlyRevealsStateAfterValidPassword_AndPreservesAthleteLogin(string language)
    {
        var applicant = await RegisterAsync();
        Client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        var wrong = await Client.PostAsJsonAsync("/api/trainer/login", new { name = applicant.Name, password = "wrong-password" });
        Assert.That(wrong.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await Body(wrong)).TryGetProperty("code", out _), Is.False);
        var correct = await Client.PostAsJsonAsync("/api/trainer/login", new { name = applicant.Name, password = "password123" });
        Assert.That(correct.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var denied = await Body(correct);
        Assert.That(denied.GetProperty("code").GetString(), Is.EqualTo("TrainerPendingApproval"));
        Assert.That(denied.GetProperty("msg").GetString(), Does.Contain(language == "pl" ? "zatwierdzenie" : "approval"));
        using (var scope = Factory.Services.CreateScope())
            Assert.That((await scope.ServiceProvider.GetRequiredService<AppDbContext>().UserSessions.CountAsync()), Is.EqualTo(0));
        var athlete = await Client.PostAsJsonAsync("/api/login", new { name = applicant.Name, password = "password123" });
        Assert.That(athlete.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var data = await Body(athlete);
        Assert.That(data.GetProperty("req").GetProperty("trainerApprovalStatus").GetString(), Is.EqualTo("PendingApproval"));
        Assert.That(data.GetProperty("permissionClaims").EnumerateArray().Select(value => value.GetString()), Does.Not.Contain("trainer.access"));
    }

    [TestCase(TrainerApprovalStatus.PendingApproval)]
    [TestCase(TrainerApprovalStatus.Rejected)]
    public async Task OldToken_AndInjectedTrainerRole_CannotUseAnyTrainerPolicyRoute(TrainerApprovalStatus status)
    {
        var applicant = await RegisterAsync();
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var application = await db.TrainerApplications.SingleAsync(item => item.ApplicantId == applicant.Id);
            application.Status = status;
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
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), $"{method} {path}");
                tested++;
            }
        }
        Assert.That(tested, Is.GreaterThan(20));
        Assert.That((await Client.GetAsync("/api/checkToken")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [TestCase("approve")]
    [TestCase("reject")]
    public async Task Review_DeniesAnonymousAthleteSelfRevokedAdminAndInvalidApplicants(string action)
    {
        var applicant = await RegisterAsync();
        var url = $"/api/admin/users/{applicant.Id}/trainer-application/{action}";
        Assert.That((await Client.PostAsync(url, null)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        SetAuthorizationHeader(applicant.Id);
        Assert.That((await Client.PostAsync(url, null)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var admin = await SeedAdminAsync();
        SetAuthorizationHeader(admin.Id);
        Assert.That((await Client.PostAsync($"/api/admin/users/{admin.Id}/trainer-application/{action}", null)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That((await Client.PostAsync($"/api/admin/users/not-an-id/trainer-application/{action}", null)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await Client.PostAsync($"/api/admin/users/{LgymApi.Domain.ValueObjects.Id<User>.New()}/trainer-application/{action}", null)).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var ordinary = await SeedUserAsync("ordinary", "ordinary@example.test");
        Assert.That((await Client.PostAsync($"/api/admin/users/{ordinary.Id}/trainer-application/{action}", null)).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserRoles.RemoveRange(await db.UserRoles.Where(role => role.UserId == admin.Id && role.Role.Name == "Admin").ToListAsync());
            await db.SaveChangesAsync();
        }
        Assert.That((await Client.PostAsync(url, null)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That((await Client.GetAsync("/api/admin/users/trainer-applications")).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task GenericRoles_CannotGrantTrainerApproval_OrSmuggleTrainerPermission()
    {
        var applicant = await RegisterAsync();
        var admin = await SeedAdminAsync();
        SetAuthorizationHeader(admin.Id);
        var direct = await Client.PostAsJsonAsync($"/api/roles/users/{applicant.Id}/roles", new { roles = UserAndTrainerRoles });
        Assert.That(direct.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await Body(direct)).GetProperty("code").GetString(), Is.EqualTo("TrainerApprovalRequired"));
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userRole = await db.Roles.Include(role => role.RoleClaims).SingleAsync(role => role.Name == "User");
            userRole.RoleClaims.Add(new RoleClaim { Id = LgymApi.Domain.ValueObjects.Id<RoleClaim>.New(),
                RoleId = userRole.Id, ClaimType = AuthConstants.PermissionClaimType, ClaimValue = AuthConstants.Permissions.TrainerAccess });
            await db.SaveChangesAsync();
        }
        SetAuthorizationHeader(applicant.Id);
        Assert.That((await Client.GetAsync("/api/trainer/checkToken")).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var profile = await Body(await Client.GetAsync("/api/checkToken"));
        Assert.That(profile.GetProperty("permissionClaims").EnumerateArray().Select(value => value.GetString()), Does.Not.Contain("trainer.access"));
    }

    [Test]
    public async Task RepeatedRegistration_ReplaysPendingConfirmationWithoutDuplicatingAccountOrSession()
    {
        var applicant = await RegisterAsync();
        var repeated = await RegisterAsync();
        Assert.That(repeated.Id, Is.EqualTo(applicant.Id));
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.That((await db.Users.CountAsync()), Is.EqualTo(1));
        Assert.That((await db.TrainerApplications.CountAsync()), Is.EqualTo(1));
        Assert.That((await db.UserSessions.CountAsync()), Is.EqualTo(0));
        Assert.That((await db.UserRoles.AnyAsync(role => role.Role.Name == "Trainer")), Is.False);
    }

    private async Task<User> RegisterAsync()
    {
        SetIdempotencyKey("trainer-approval-registration");
        var response = await Client.PostAsJsonAsync("/api/trainer/register", new
        { name = "applicant", email = "applicant@example.test", password = "password123", cpassword = "password123" });
        ClearIdempotencyKey();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AsNoTracking().SingleAsync(user => user.Name == "applicant");
    }

    private static async Task<JsonElement> Body(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
}
