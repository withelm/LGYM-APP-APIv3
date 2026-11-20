using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.Application.Platform.Contracts.Serialization;
using LgymApi.Domain.Notifications;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class UserAuthTests : IntegrationTestBase
{
    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/register", "public", "anonymous-intended-behavior")]
    public async Task Register_WithValidData_ReturnsCreatedAndCreatesUserWithElo()
    {
        var request = new
        {
            name = "newuser",
            email = "newuser@example.com",
            password = "password123",
            cpassword = "password123",
            isVisibleInRanking = true
        };

        SetIdempotencyKey("test-register-newuser");
        var response = await Client.PostAsJsonAsync("/api/register", request);
        ClearIdempotencyKey();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo("Created"));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Name == "newuser");
        Assert.That(user, Is.Not.Null);
        Assert.That(user!.Email.Value, Is.EqualTo("newuser@example.com"));
        Assert.That(user.ProfileRank, Is.EqualTo("Junior 1"));
        Assert.That(user.IsVisibleInRanking, Is.True);

        var eloEntry = await db.EloRegistries.FirstOrDefaultAsync(e => e.UserId == user.Id);
        Assert.That(eloEntry, Is.Not.Null);
        Assert.That(eloEntry!.Elo.Value, Is.EqualTo(1000));
    }

    [Test]
    public async Task Register_WithNoLanguageSpecified_SendsEnglishWelcomeEmail()
    {
        var request = new
        {
            name = "bob-en",
            email = "bob-en@example.com",
            password = "securepass",
            cpassword = "securepass",
            isVisibleInRanking = true
        };

        SetIdempotencyKey("test-register-bob-en");
        var response = await Client.PostAsJsonAsync("/api/register", request);
        ClearIdempotencyKey();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        await ProcessPendingCommandsAsync();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Name == "bob-en");
        Assert.That(user, Is.Not.Null);
        Assert.That(user!.PreferredLanguage, Does.StartWith("en"));

        var emailLog = await db.NotificationMessages
            .OrderByDescending(e => e.SentAt ?? e.LastAttemptAt)
            .FirstOrDefaultAsync(e => e.Recipient == "bob-en@example.com" && e.Type == EmailNotificationTypes.Welcome);
        Assert.That(emailLog, Is.Not.Null);

        var payload = System.Text.Json.JsonSerializer.Deserialize<LgymApi.BackgroundWorker.Common.Notifications.Models.WelcomeEmailPayload>(emailLog!.PayloadJson, SharedSerializationOptions.Current);
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.CultureName, Does.StartWith("en"));
        Assert.That(emailLog.PayloadJson, Does.Contain("en"));
    }
    [Test]
    public async Task Register_WithAcceptLanguage_Header_PlSendsPolishWelcomeEmail()
    {
        var request = new
        {
            name = "alicja-hdr",
            email = "alicja-hdr@example.com",
            password = "securepass",
            cpassword = "securepass",
            isVisibleInRanking = true
        };

        var msg = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "/api/register")
        {
            Content = System.Net.Http.Json.JsonContent.Create(request)
        };
        msg.Headers.Add("Accept-Language", "pl;q=1.0");
        msg.Headers.Add("Idempotency-Key", "test-register-alicja-hdr");

        var response = await Client.SendAsync(msg);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        await ProcessPendingCommandsAsync();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Name == "alicja-hdr");
        Assert.That(user, Is.Not.Null);
        Assert.That(user!.PreferredLanguage, Does.StartWith("pl"));

        var emailLog = await db.NotificationMessages
            .OrderByDescending(e => e.SentAt ?? e.LastAttemptAt)
            .FirstOrDefaultAsync(e => e.Recipient == "alicja-hdr@example.com" && e.Type == EmailNotificationTypes.Welcome);
        Assert.That(emailLog, Is.Not.Null);

        var payload = System.Text.Json.JsonSerializer.Deserialize<LgymApi.BackgroundWorker.Common.Notifications.Models.WelcomeEmailPayload>(emailLog!.PayloadJson, SharedSerializationOptions.Current);
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.CultureName, Does.StartWith("pl"));

        // The Polish template has a distinct subject and body.
        Assert.That(emailLog.PayloadJson, Does.Contain("pl"));
        // Optionally check the subject/body as needed.
    }
    [Test]
    public async Task Register_WithPreferredLanguage_PlSendsPolishWelcomeEmail()
    {
        var request = new
        {
            name = "alicja-pl",
            email = "alicja-pl@example.com",
            password = "securepass",
            cpassword = "securepass",
            isVisibleInRanking = true
        };

        var msg = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "/api/register")
        {
            Content = System.Net.Http.Json.JsonContent.Create(request)
        };
        msg.Headers.Add("Accept-Language", "pl-PL;q=1.0");
        msg.Headers.Add("Idempotency-Key", "test-register-alicja-pl");

        var response = await Client.SendAsync(msg);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        await ProcessPendingCommandsAsync();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Name == "alicja-pl");
        Assert.That(user, Is.Not.Null);
        Assert.That(user!.PreferredLanguage, Is.EqualTo("pl-PL"));

        var emailLog = await db.NotificationMessages
            .OrderByDescending(e => e.SentAt ?? e.LastAttemptAt)
            .FirstOrDefaultAsync(e => e.Recipient == "alicja-pl@example.com" && e.Type == EmailNotificationTypes.Welcome);
        Assert.That(emailLog, Is.Not.Null);

        var payload = System.Text.Json.JsonSerializer.Deserialize<LgymApi.BackgroundWorker.Common.Notifications.Models.WelcomeEmailPayload>(emailLog!.PayloadJson, SharedSerializationOptions.Current);
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.CultureName, Is.EqualTo("pl-PL"));

        // The Polish template has a distinct subject and body.
        Assert.That(emailLog.PayloadJson, Does.Contain("pl-PL"));
        // Optionally check the subject/body if stored; else, template test ensures content.
    }
    [TestCase("", "test@example.com", "password123", "password123", TestName = "Register_WithEmptyName_ReturnsError")]
    [TestCase("testuser", "invalid-email", "password123", "password123", TestName = "Register_WithInvalidEmail_ReturnsError")]
    [TestCase("testuser", "test@example.com", "12345", "12345", TestName = "Register_WithShortPassword_ReturnsError")]
    [TestCase("testuser", "test@example.com", "password123", "differentpassword", TestName = "Register_WithMismatchedPasswords_ReturnsError")]
    public async Task Register_WithInvalidData_ReturnsBadRequest(string name, string email, string password, string cpassword)
    {
        var request = new
        {
            name,
            email,
            password,
            cpassword
        };

        SetIdempotencyKey($"test-register-invalid-{name}-{email}");
        var response = await Client.PostAsJsonAsync("/api/register", request);
        ClearIdempotencyKey();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Register_WithExistingName_ReturnsConflict()
    {
        await SeedUserAsync(name: "existinguser", email: "existing@example.com");

        var request = new
        {
            name = "existinguser",
            email = "new@example.com",
            password = "password123",
            cpassword = "password123"
        };

        SetIdempotencyKey("test-register-existingname");
        var response = await Client.PostAsJsonAsync("/api/register", request);
        ClearIdempotencyKey();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo("A user with that name already exists."));
    }

    [Test]
    public async Task Register_WithExistingEmail_ReturnsConflict()
    {
        await SeedUserAsync(name: "existinguser", email: "existing@example.com");

        var request = new
        {
            name = "newuser",
            email = "existing@example.com",
            password = "password123",
            cpassword = "password123"
        };

        SetIdempotencyKey("test-register-existingemail");
        var response = await Client.PostAsJsonAsync("/api/register", request);
        ClearIdempotencyKey();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo("A user with that email already exists."));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/login", "public", "anonymous-intended-behavior")]
    public async Task Login_WithValidCredentials_ReturnsTokenAndUserInfo()
    {
        await SeedUserAsync(name: "loginuser", email: "login@example.com", password: "mypassword", elo: 1500);

        var request = new
        {
            name = "loginuser",
            password = "mypassword"
        };

        var response = await Client.PostAsJsonAsync("/api/login", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(string.IsNullOrWhiteSpace(body!.Token), Is.False);
        Assert.That(body.User, Is.Not.Null);
        Assert.That(body.User!.Name, Is.EqualTo("loginuser"));
        Assert.That(body.User.Email, Is.EqualTo("login@example.com"));
        Assert.That(body.User.Elo, Is.EqualTo(1500));
        Assert.That(body.User.ProfileRank, Is.EqualTo("Junior 1"));
        Assert.That(body.User.IsVisibleInRanking, Is.True);
    }

    [Test]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        await SeedUserAsync(name: "loginuser", email: "login@example.com", password: "correctpassword");

        var request = new
        {
            name = "loginuser",
            password = "wrongpassword"
        };

        var response = await Client.PostAsJsonAsync("/api/login", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo("Unauthorized"));
    }

    [Test]
    public async Task Login_WithNonExistentUser_ReturnsUnauthorized()
    {
        var request = new
        {
            name = "nonexistent",
            password = "somepassword"
        };

        var response = await Client.PostAsJsonAsync("/api/login", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo("Unauthorized"));
    }

    [Test]
    public async Task Login_WithEmptyCredentials_ReturnsBadRequest()
    {
        var request = new
        {
            name = "",
            password = ""
        };

        var response = await Client.PostAsJsonAsync("/api/login", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/checkToken", "own", "owner-allow")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/checkToken", "own", "no-client-subject")]
    public async Task CheckToken_WithValidToken_ReturnsUserInfo()
    {
        var user = await SeedUserAsync(name: "tokenuser", email: "token@example.com", elo: 2000);
        SetAuthorizationHeader(user.Id);

        var response = await Client.GetAsync("/api/checkToken");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<UserInfoResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Name, Is.EqualTo("tokenuser"));
        Assert.That(body.Email, Is.EqualTo("token@example.com"));
        Assert.That(body.Elo, Is.EqualTo(2000));
    }

    [Test]
    public async Task CheckToken_WithoutToken_ReturnsUnauthorized()
    {
        ClearAuthorizationHeader();

        var response = await Client.GetAsync("/api/checkToken");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var body = await response.Content.ReadFromJsonAsync<MiddlewareErrorResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo("Invalid JWT token."));
    }

    [Test]
    public async Task CheckToken_WithInvalidToken_ReturnsUnauthorized()
    {
        Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid.jwt.token");

        var response = await Client.GetAsync("/api/checkToken");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var body = await response.Content.ReadFromJsonAsync<MiddlewareErrorResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo("Invalid JWT token."));
    }

    [Test]
    public async Task CheckToken_WithDeletedUser_ReturnsUnauthorized()
    {
        var user = await SeedUserAsync(name: "deleteduser", email: "deleted@example.com", isDeleted: true);
        SetAuthorizationHeader(user.Id);

        var response = await Client.GetAsync("/api/checkToken");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var body = await response.Content.ReadFromJsonAsync<MiddlewareErrorResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo("Unauthorized"));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/logout", "own", "owner-allow")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/logout", "own", "no-client-subject")]
    public async Task Logout_InvalidatesCurrentJwt()
    {
        await SeedUserAsync(name: "logoutuser", email: "logout@example.com", password: "logoutpass");

        var loginResponse = await Client.PostAsJsonAsync("/api/login", new
        {
            name = "logoutuser",
            password = "logoutpass"
        });
        Assert.That(loginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(loginBody, Is.Not.Null);

        Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginBody!.Token);

        var logoutResponse = await Client.PostAsync("/api/logout", null);
        Assert.That(logoutResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var checkTokenResponse = await Client.GetAsync("/api/checkToken");
        Assert.That(checkTokenResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var body = await checkTokenResponse.Content.ReadFromJsonAsync<MiddlewareErrorResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo("Unauthorized"));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/deleteAccount", "own", "owner-allow")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/deleteAccount", "own", "no-client-subject")]
    public async Task DeleteAccount_WithValidToken_PreservesIdentityForRecoveryAndReturnsDeleted()
    {
        var user = await SeedUserAsync(name: "todelete", email: "todelete@example.com");
        SetAuthorizationHeader(user.Id);

        var response = await Client.GetAsync("/api/deleteAccount");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo("Deleted."));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updatedUser = await db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == user.Id);

        Assert.That(updatedUser, Is.Not.Null);
        Assert.That(updatedUser!.IsDeleted, Is.False);
        Assert.That(updatedUser.Name, Is.EqualTo("todelete"));
        Assert.That(updatedUser.Email.Value, Is.EqualTo("todelete@example.com"));
        Assert.That(updatedUser.LegacyHash, Is.EqualTo(user.LegacyHash));
        Assert.That(updatedUser.LegacySalt, Is.EqualTo(user.LegacySalt));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/updateTimeZone", "own", "owner-allow")]
    public async Task UpdateTimeZone_WithAuthenticatedUser_ReturnsOk()
    {
        var user = await SeedUserAsync(name: "timezone-user", email: "timezone-user@example.com");
        SetAuthorizationHeader(user.Id);

        var response = await Client.PostAsJsonAsync("/api/updateTimeZone", new { preferredTimeZone = "UTC" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    private sealed class MessageResponse
    {
        [JsonPropertyName("msg")]
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Used for middleware error responses which use { "msg": "..." } format
    /// </summary>
    private sealed class MiddlewareErrorResponse
    {
        [JsonPropertyName("msg")]
        public string Message { get; set; } = string.Empty;
    }

    private sealed class LoginResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("req")]
        public UserInfoResponse? User { get; set; }
    }

    private sealed class UserInfoResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("elo")]
        public int Elo { get; set; }

        [JsonPropertyName("profileRank")]
        public string ProfileRank { get; set; } = string.Empty;

        [JsonPropertyName("isVisibleInRanking")]
        public bool IsVisibleInRanking { get; set; }
    }
}
