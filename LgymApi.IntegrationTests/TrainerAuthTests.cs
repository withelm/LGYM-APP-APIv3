using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class TrainerAuthTests : IntegrationTestBase
{
    private static readonly string[] ExpectedTrainerRoles = ["User"];

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/trainer/register", "public", "anonymous-intended-behavior")]
    public async Task RegisterTrainer_WithValidData_CreatesPendingApplication()
    {
        var request = new
        {
            name = "trainer-one",
            email = "trainer-one@example.com",
            password = "password123",
            cpassword = "password123"
        };

        SetIdempotencyKey("test-register-trainer-one");
        var response = await Client.PostAsJsonAsync("/api/trainer/register", request);
        ClearIdempotencyKey();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Name == "trainer-one");
        Assert.That(user, Is.Not.Null);

        var roleNames = await db.UserRoles
            .Where(ur => ur.UserId == user!.Id)
            .Select(ur => ur.Role.Name)
            .ToListAsync();

        Assert.That(roleNames, Is.EquivalentTo(ExpectedTrainerRoles).UsingPropertiesComparer());
        var application = await db.TrainerApplications.SingleAsync(item => item.ApplicantId == user!.Id);
        Assert.That(application.Status, Is.EqualTo(LgymApi.Domain.Enums.TrainerApprovalStatus.PendingApproval));
        Assert.That(application.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
        Assert.That((await db.UserSessions.CountAsync()), Is.EqualTo(0));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/trainer/login", "public", "anonymous-intended-behavior")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/trainer/checkToken", "own", "owner-allow")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/trainer/checkToken", "own", "no-client-subject")]
    public async Task TrainerCheckToken_WithTrainerToken_ReturnsOk()
    {
        SetIdempotencyKey("test-register-trainer-auth");
        await Client.PostAsJsonAsync("/api/trainer/register", new
        {
            name = "trainer-auth",
            email = "trainer-auth@example.com",
            password = "password123",
            cpassword = "password123"
        });
        ClearIdempotencyKey();

        var admin = await SeedAdminAsync();
        SetAuthorizationHeader(admin.Id);
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var applicant = await db.Users.SingleAsync(user => user.Name == "trainer-auth");
            var approval = await Client.PostAsync($"/api/admin/users/{applicant.Id}/trainer-application/approve", null);
            Assert.That(approval.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
        ClearAuthorizationHeader();

        var loginResponse = await Client.PostAsJsonAsync("/api/trainer/login", new
        {
            name = "trainer-auth",
            password = "password123"
        });

        Assert.That(loginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(loginBody, Is.Not.Null);
        Assert.That(string.IsNullOrWhiteSpace(loginBody!.Token), Is.False);

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody.Token);

        var response = await Client.GetAsync("/api/trainer/checkToken");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task TrainerCheckToken_WithRegularUserToken_ReturnsForbidden()
    {
        var user = await SeedUserAsync(name: "regular-user", email: "regular-user@example.com", password: "password123");
        SetAuthorizationHeader(user.Id);

        var response = await Client.GetAsync("/api/trainer/checkToken");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task TrainerLogin_WithNonTrainerUser_ReturnsUnauthorized()
    {
        await SeedUserAsync(name: "mobile-user", email: "mobile-user@example.com", password: "password123");

        var response = await Client.PostAsJsonAsync("/api/trainer/login", new
        {
            name = "mobile-user",
            password = "password123"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    private sealed class LoginResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;
    }
}
