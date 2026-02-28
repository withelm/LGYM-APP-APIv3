using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.BackgroundWorker;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
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

    // Default admin credentials for tests
    protected const string AdminName = "testadmin";
    protected const string AdminEmail = "testadmin@example.com";
    protected const string AdminPassword = "AdminPass123!";

    protected async Task<User> SeedAdminAsync()
    {
        return await SeedUserAsync(
            name: AdminName,
            email: AdminEmail,
            password: AdminPassword,
            isAdmin: true);
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
            IsVisibleInRanking = isVisibleInRanking,
            IsDeleted = isDeleted,
            ProfileRank = "Junior 1",
            LegacyHash = passwordData.Hash,
            LegacySalt = passwordData.Salt,
            LegacyIterations = passwordData.Iterations,
            LegacyKeyLength = passwordData.KeyLength,
            LegacyDigest = passwordData.Digest
        };

        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = AppDbContext.UserRoleSeedId });
        if (isAdmin)
        {
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = AppDbContext.AdminRoleSeedId });
        }

        if (isTester)
        {
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = AppDbContext.TesterRoleSeedId });
        }

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
        using var scope = Factory.Services.CreateScope();
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

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(CustomWebApplicationFactory.TestJwtSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim("userId", userId.ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        foreach (var permission in permissionClaims)
        {
            claims.Add(new Claim(AuthConstants.PermissionClaimType, permission));
        }

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddDays(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected void SetAuthorizationHeader(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var userSessionCache = scope.ServiceProvider.GetRequiredService<IUserSessionCache>();
        userSessionCache.AddOrRefresh(userId);

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

    protected async Task<(Guid UserId, string Token)> RegisterUserViaEndpointAsync(
        string name = "testuser",
        string email = "test@example.com",
        string password = "password123",
        bool isVisibleInRanking = true)
    {
        var registerRequest = new
        {
            name,
            email,
            password,
            cpassword = password,
            isVisibleInRanking
        };

        await Client.PostAsJsonAsync("/api/register", registerRequest);

        var loginRequest = new { name, password };
        var loginResponse = await Client.PostAsJsonAsync("/api/login", loginRequest);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();

        return (Guid.Parse(loginBody!.User!.Id!), loginBody.Token!);
    }

    protected async Task<Guid> CreateGymViaEndpointAsync(Guid userId, string name = "Test Gym")
    {
        SetAuthorizationHeader(userId);
        var request = new { name, address = (string?)null };
        await Client.PostAsJsonAsync($"/api/gym/{userId}/addGym", request);

        var gymsResponse = await Client.GetAsync($"/api/gym/{userId}/getGyms");
        var gyms = await gymsResponse.Content.ReadFromJsonAsync<List<GymResult>>();
        return Guid.Parse(gyms!.First(g => g.Name == name).Id!);
    }

    protected async Task<Guid> CreatePlanViaEndpointAsync(Guid userId, string name = "Test Plan")
    {
        SetAuthorizationHeader(userId);
        var request = new { name };
        await Client.PostAsJsonAsync($"/api/{userId}/createPlan", request);

        var plansResponse = await Client.GetAsync($"/api/{userId}/getPlansList");
        var plans = await plansResponse.Content.ReadFromJsonAsync<List<PlanResult>>();
        return Guid.Parse(plans!.First(p => p.Name == name).Id!);
    }

    protected async Task<Guid> CreateExerciseViaEndpointAsync(Guid userId, string name = "Test Exercise", BodyParts bodyPart = BodyParts.Chest)
    {
        SetAuthorizationHeader(userId);
        var request = new { name, bodyPart = bodyPart.ToString(), description = "Test description" };
        await PostAsJsonWithApiOptionsAsync($"/api/exercise/{userId}/addUserExercise", request);

        var exercisesResponse = await Client.GetAsync($"/api/exercise/{userId}/getAllUserExercises");
        var exercises = await exercisesResponse.Content.ReadFromJsonAsync<List<ExerciseResult>>();
        return Guid.Parse(exercises!.First(e => e.Name == name).Id!);
    }

    protected async Task<Guid> CreateGlobalExerciseViaEndpointAsync(Guid userId, string name = "Global Exercise", BodyParts bodyPart = BodyParts.Chest)
    {
        SetAuthorizationHeader(userId);
        var request = new { name, bodyPart = bodyPart.ToString(), description = "Global exercise description" };
        await Client.PostAsJsonAsync("/api/exercise/addExercise", request);

        var exercisesResponse = await Client.GetAsync("/api/exercise/getAllGlobalExercises");
        var exercises = await exercisesResponse.Content.ReadFromJsonAsync<List<ExerciseResult>>();
        return Guid.Parse(exercises!.First(e => e.Name == name).Id!);
    }

    protected async Task<Guid> CreatePlanDayViaEndpointAsync(Guid userId, Guid planId, string name, List<PlanDayExerciseInput> exercises)
    {
        SetAuthorizationHeader(userId);
        var request = new { name, exercises };
        await PostAsJsonWithApiOptionsAsync($"/api/planDay/{planId}/createPlanDay", request);

        var planDaysResponse = await Client.GetAsync($"/api/planDay/{planId}/getPlanDays");
        var planDays = await planDaysResponse.Content.ReadFromJsonAsync<List<PlanDayResult>>();
        return Guid.Parse(planDays!.First(pd => pd.Name == name).Id!);
    }

    protected sealed class LoginResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("token")]
        public string? Token { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("req")]
        public UserResult? User { get; set; }
    }

    protected sealed class UserResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("_id")]
        public string? Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    protected sealed class GymResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("_id")]
        public string? Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    protected sealed class PlanResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("_id")]
        public string? Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    protected sealed class ExerciseResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("_id")]
        public string? Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    protected sealed class PlanDayResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("_id")]
        public string? Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    protected sealed class PlanDayExerciseInput
    {
        [System.Text.Json.Serialization.JsonPropertyName("exercise")]
        public string? ExerciseId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("series")]
        public int Series { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("reps")]
        public string Reps { get; set; } = string.Empty;
    }

    protected async Task ProcessPendingCommandsAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<BackgroundActionOrchestratorService>();

        // Get all pending command envelopes from database
        var pendingEnvelopes = await db.CommandEnvelopes
            .Where(ce => ce.Status == ActionExecutionStatus.Pending)
            .ToListAsync();

        // Process each pending envelope
        foreach (var envelope in pendingEnvelopes)
        {
            try
            {
                await orchestrator.OrchestrateAsync(envelope.Id, CancellationToken.None);
            }
            catch
            {
                // Suppress errors to allow tests to continue
                // (mimics Hangfire behavior where job failures don't stop other processing)
            }
        }
    }

}