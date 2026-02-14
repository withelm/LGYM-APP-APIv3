using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace LgymApi.IntegrationTests;

public abstract class IntegrationTestBase : IDisposable
{
    protected CustomWebApplicationFactory Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;

    [SetUp]
    public void SetUp()
    {
        Factory = new CustomWebApplicationFactory();
        Client = Factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        Client.Dispose();
        
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureDeleted();
        
        Factory.Dispose();
    }

    public void Dispose()
    {
        Client?.Dispose();
        Factory?.Dispose();
        GC.SuppressFinalize(this);
    }

    protected async Task<User> SeedUserAsync(
        string name = "testuser",
        string email = "test@example.com",
        string password = "password123",
        bool isAdmin = false,
        bool isVisibleInRanking = true,
        bool isTester = false,
        bool isDeleted = false,
        int elo = 1000)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordService = new LegacyPasswordService();

        var passwordData = passwordService.Create(password);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = email,
            Admin = isAdmin,
            IsVisibleInRanking = isVisibleInRanking,
            IsTester = isTester,
            IsDeleted = isDeleted,
            ProfileRank = "Junior 1",
            LegacyHash = passwordData.Hash,
            LegacySalt = passwordData.Salt,
            LegacyIterations = passwordData.Iterations,
            LegacyKeyLength = passwordData.KeyLength,
            LegacyDigest = passwordData.Digest
        };

        db.Users.Add(user);

        var eloRegistry = new EloRegistry
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Date = DateTimeOffset.UtcNow,
            Elo = elo
        };
        db.EloRegistries.Add(eloRegistry);

        await db.SaveChangesAsync();

        return user;
    }

    protected string GenerateJwt(Guid userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(CustomWebApplicationFactory.TestJwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim("userId", userId.ToString())
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddDays(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected void SetAuthorizationHeader(Guid userId)
    {
        var token = GenerateJwt(userId);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    protected void ClearAuthorizationHeader()
    {
        Client.DefaultRequestHeaders.Authorization = null;
    }

    protected AppDbContext GetDbContext()
    {
        var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    protected Task<HttpResponseMessage> PostAsJsonWithApiOptionsAsync<T>(string requestUri, T value)
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        var json = System.Text.Json.JsonSerializer.Serialize(value, options);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        return Client.PostAsync(requestUri, content);
    }
}
