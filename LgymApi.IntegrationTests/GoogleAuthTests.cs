using System.Net;
using System.Net.Http.Json;
using System.IO;
using FluentAssertions;
using LgymApi.Application.Services;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.TestUtils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class GoogleAuthTests : IntegrationTestBase
{
    private IGoogleTokenValidator _googleTokenValidator = null!;
    private CustomWebApplicationFactory _baseFactory = null!;
    private HttpClient _client = null!;
    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory = null!;

    [SetUp]
    public void SetUpGoogleAuthHost()
    {
        _googleTokenValidator = Substitute.For<IGoogleTokenValidator>();

        _baseFactory = new CustomWebApplicationFactory();
        _factory = _baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGoogleTokenValidator>();
                services.AddSingleton(_googleTokenValidator);
            });
        });

        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDownGoogleAuthHost()
    {
        _client.Dispose();
        _factory.Dispose();
        _baseFactory.Dispose();
    }

    [Test]
    public async Task POST_AuthGoogle_ValidToken_NewUser_Returns200_AndCreatesUser()
    {
        const string email = "google-new@example.com";
        const string subject = "google-sub-new";

        _googleTokenValidator.ValidateAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleTokenPayload(subject, email, true, "Google New", null));

        var response = await _client.PostAsJsonAsync("/api/auth/google", new { idToken = "valid-token" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrWhiteSpace();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await db.Users.FirstOrDefaultAsync(x => x.Email.Value == email);
        user.Should().NotBeNull();

        var externalLogin = await db.UserExternalLogins.FirstOrDefaultAsync(x => x.UserId == user!.Id && x.Provider == AuthConstants.ExternalProviders.Google);
        externalLogin.Should().NotBeNull();
        externalLogin!.ProviderEmail.Should().Be(email);
    }

    [Test]
    public async Task POST_AuthGoogle_ValidToken_ExistingLinkedUser_Returns200()
    {
        const string email = "google-linked@example.com";
        const string subject = "google-sub-linked";

        var user = await SeedUserAsync(name: "linkeduser", email: email);
        await SeedGoogleExternalLoginAsync(user.Id, subject, email);

        _googleTokenValidator.ValidateAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleTokenPayload(subject, email, true, "Google Linked", null));

        var response = await _client.PostAsJsonAsync("/api/auth/google", new { idToken = "valid-token" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrWhiteSpace();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        (await db.Users.CountAsync(x => x.Email.Value == email)).Should().Be(1);
    }

    [Test]
    public async Task POST_AuthGoogle_EmailCollision_NoLink_Returns409()
    {
        const string email = "collision@example.com";

        await SeedUserAsync(name: "localuser", email: email);

        _googleTokenValidator.ValidateAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleTokenPayload("google-sub-collision", email, true, "Google Collision", null));

        var response = await _client.PostAsJsonAsync("/api/auth/google", new { idToken = "valid-token" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task POST_AuthGoogle_InvalidToken_Returns401()
    {
        _googleTokenValidator.ValidateAsync("invalid-token", Arg.Any<CancellationToken>())
            .Returns((GoogleTokenPayload?)null);

        var response = await _client.PostAsJsonAsync("/api/auth/google", new { idToken = "invalid-token" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task POST_AuthGoogle_UnverifiedEmail_Returns401()
    {
        _googleTokenValidator.ValidateAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleTokenPayload("google-sub-unverified", "unverified@example.com", false, "Google Unverified", null));

        var response = await _client.PostAsJsonAsync("/api/auth/google", new { idToken = "valid-token" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task POST_AuthGoogle_MissingIdToken_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/google", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task POST_LinkGoogle_Authenticated_Success_Returns200()
    {
        const string email = "link-success@example.com";
        const string subject = "google-sub-link-success";

        var user = await SeedUserAsync(name: "linkuser", email: email);
        SetAuthorizationHeader(user.Id);

        _googleTokenValidator.ValidateAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleTokenPayload(subject, email, true, "Google Link", null));

        var response = await _client.PostAsJsonAsync("/api/account/link-google", new { idToken = "valid-token" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var externalLogin = await db.UserExternalLogins.FirstOrDefaultAsync(x => x.UserId == user.Id && x.Provider == AuthConstants.ExternalProviders.Google);
        externalLogin.Should().NotBeNull();
        externalLogin!.ProviderEmail.Should().Be(email);
    }

    [Test]
    public async Task POST_LinkGoogle_Unauthenticated_Returns401()
    {
        _googleTokenValidator.ValidateAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleTokenPayload("google-sub-unauth", "unauth@example.com", true, "Google Unauth", null));

        var response = await _client.PostAsJsonAsync("/api/account/link-google", new { idToken = "valid-token" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task POST_LinkGoogle_AlreadyLinkedGoogleAccount_Returns409()
    {
        const string email = "linked-other@example.com";
        const string subject = "google-sub-conflict";

        var otherUser = await SeedUserAsync(name: "otheruser", email: email);
        await SeedGoogleExternalLoginAsync(otherUser.Id, subject, email);

        var currentUser = await SeedUserAsync(name: "currentuser", email: "current@example.com");
        SetAuthorizationHeader(currentUser.Id);

        _googleTokenValidator.ValidateAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleTokenPayload(subject, "current@example.com", true, "Google Conflict", null));

        var response = await _client.PostAsJsonAsync("/api/account/link-google", new { idToken = "valid-token" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task GET_ExternalLogins_Authenticated_ReturnsProviders()
    {
        const string email = "external-list@example.com";

        var user = await SeedUserAsync(name: "listuser", email: email);
        await SeedGoogleExternalLoginAsync(user.Id, "google-sub-list", email);
        SetAuthorizationHeader(user.Id);

        var response = await _client.GetAsync("/api/account/external-logins");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<ExternalLoginDto>>();
        body.Should().NotBeNull();
        body.Should().ContainSingle();
        body![0].Provider.Should().Be(AuthConstants.ExternalProviders.Google);
        body[0].ProviderEmail.Should().Be(email);
    }

    [Test]
    public async Task GET_ExternalLogins_Unauthenticated_Returns401()
    {
        var response = await _client.GetAsync("/api/account/external-logins");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task SeedGoogleExternalLoginAsync(Id<User> userId, string subject, string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.UserExternalLogins.Add(new UserExternalLogin
        {
            Id = Id<UserExternalLogin>.New(),
            UserId = userId,
            Provider = AuthConstants.ExternalProviders.Google,
            ProviderKey = subject,
            ProviderEmail = email,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            IsDeleted = false
        });

        await db.SaveChangesAsync();
    }

    private new async Task<User> SeedUserAsync(
        string name = "testuser",
        string email = "test@example.com",
        string password = "password123",
        bool isAdmin = false,
        bool isVisibleInRanking = true,
        bool isTester = false,
        bool isDeleted = false,
        int elo = 1000)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await TestDataFactory.SeedUserAsync(
            db,
            name,
            email,
            password,
            isAdmin,
            isVisibleInRanking,
            isTester,
            isDeleted,
            elo);
        await db.SaveChangesAsync();
        return user;
    }

    private new void SetAuthorizationHeader(Id<User> userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var sessionId = Id<UserSession>.New();
        var jti = Id<UserSession>.New().ToString();
        db.UserSessions.Add(new UserSession
        {
            Id = sessionId,
            UserId = userId,
            Jti = jti,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(1),
            RevokedAtUtc = null
        });
        db.SaveChanges();

        var token = GenerateJwt(userId, sessionId, jti);
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private new string GenerateJwt(Id<User> userId, Id<UserSession> sessionId, string jti)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var roles = db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.Name)
            .Distinct()
            .ToList();

        var permissionClaims = db.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RoleClaims)
            .Where(rc => rc.ClaimType == AuthConstants.PermissionClaimType)
            .Select(rc => rc.ClaimValue)
            .Distinct()
            .ToList();

        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(CustomWebApplicationFactory.TestJwtSigningKey));
        var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, userId.ToString()),
            new("userId", userId.ToString()),
            new("sid", sessionId.ToString()),
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti, jti)
        };

        foreach (var role in roles)
        {
            claims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role));
        }

        foreach (var permission in permissionClaims)
        {
            claims.Add(new System.Security.Claims.Claim(AuthConstants.PermissionClaimType, permission));
        }

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddDays(1),
            signingCredentials: credentials);

        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class LoginResponseDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;
    }

    private sealed class ExternalLoginDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("provider")]
        public string Provider { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("providerEmail")]
        public string? ProviderEmail { get; set; }
    }

}
