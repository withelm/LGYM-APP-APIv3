using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LgymApi.Api.Authorization;
using LgymApi.Api.Extensions;
using LgymApi.Application.Platform.Contracts.Serialization;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Resources;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;
using MvcJsonOptions = Microsoft.AspNetCore.Mvc.JsonOptions;

namespace LgymApi.IntegrationTests;

[TestFixture]
[NonParallelizable]
public sealed class ApiHostConfigurationCharacterizationTests
{
    private static readonly IReadOnlyDictionary<string, string> ExpectedPolicies =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AuthConstants.Policies.AdminAccess] = AuthConstants.Permissions.AdminAccess,
            [AuthConstants.Policies.ManageUserRoles] = AuthConstants.Permissions.ManageUserRoles,
            [AuthConstants.Policies.ManageAppConfig] = AuthConstants.Permissions.ManageAppConfig,
            [AuthConstants.Policies.ManageGlobalExercises] = AuthConstants.Permissions.ManageGlobalExercises,
            [AuthConstants.Policies.TrainerAccess] = AuthConstants.Permissions.TrainerAccess
        };

    private ApiHostFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void CreateHost()
    {
        _factory = new ApiHostFactory();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void DisposeHost()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public void ControllerAndHttpJsonOptions_PreserveLegacySerializationBehavior()
    {
        var typedId = Id<JsonContractEntity>.New();
        var value = new JsonContractSample(
            "visible",
            null,
            new Dictionary<string, string> { ["PascalKey"] = "entry" },
            typedId,
            JsonContractMode.ExactName);
        var optionSets = new Dictionary<string, JsonSerializerOptions>(StringComparer.Ordinal)
        {
            ["controller"] = _factory.Services.GetRequiredService<IOptions<MvcJsonOptions>>().Value.JsonSerializerOptions,
            ["http"] = _factory.Services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions
        };

        foreach (var (optionSetName, options) in optionSets)
        {
            var json = JsonSerializer.Serialize(value, options);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            TestContext.WriteLine(optionSetName);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(root.EnumerateObject().Select(property => property.Name), Is.EqualTo(new[] { "displayName", "lookup", "entityId", "mode" }));
                Assert.That(root.GetProperty("displayName").GetString(), Is.EqualTo("visible"));
                Assert.That(root.TryGetProperty("omittedValue", out _), Is.False);
                Assert.That(root.GetProperty("lookup").EnumerateObject().Select(property => property.Name), Is.EqualTo(new[] { "pascalKey" }));
                Assert.That(root.GetProperty("entityId").GetString(), Is.EqualTo(typedId.ToString()));
                Assert.That(root.GetProperty("mode").GetString(), Is.EqualTo(nameof(JsonContractMode.ExactName)));
                Assert.That(options.Converters.OfType<TypedIdJsonConverterFactory>(), Has.Exactly(1).Items);
                Assert.That(options.Converters.OfType<JsonStringEnumConverter>(), Has.Exactly(1).Items);
                Assert.That(() => { JsonSerializer.Deserialize<JsonContractMode>("0", (options)); }, Throws.InstanceOf<JsonException>());
            }
        }

        Assert.That(JsonSerializer.Deserialize<JsonContractMode>("0", SharedSerializationOptions.Current), Is.EqualTo(JsonContractMode.ExactName));
    }

    [Test]
    public void StrictHttpJsonRegistration_ProducesEquivalentMvcAndMinimalJsonAndRejectsNumericEnums()
    {
        var typedId = Id<JsonContractEntity>.New();
        var value = new JsonContractSample(
            "visible",
            null,
            new Dictionary<string, string> { ["PascalKey"] = "entry" },
            typedId,
            JsonContractMode.ExactName);
        var services = new ServiceCollection();
        services.AddStrictHttpJsonOptions();

        using var serviceProvider = services.BuildServiceProvider();
        var mvcOptions = serviceProvider.GetRequiredService<IOptions<MvcJsonOptions>>().Value.JsonSerializerOptions;
        var httpOptions = serviceProvider.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;
        var mvcJson = JsonSerializer.Serialize(value, mvcOptions);
        var httpJson = JsonSerializer.Serialize(value, httpOptions);
        Action deserializeMvcNumericEnum = () => JsonSerializer.Deserialize<JsonContractMode>("0", mvcOptions);
        Action deserializeHttpNumericEnum = () => JsonSerializer.Deserialize<JsonContractMode>("0", httpOptions);
        var persistedEnum = JsonSerializer.Deserialize<JsonContractMode>("0", SharedSerializationOptions.Current);

        Assert.Multiple(() =>
        {
            Assert.That(mvcJson, Is.EqualTo("{\"displayName\":\"visible\",\"lookup\":{\"pascalKey\":\"entry\"},\"entityId\":\"" + typedId + "\",\"mode\":\"ExactName\"}"));
            Assert.That(httpJson, Is.EqualTo(mvcJson));
            Assert.That(() => { deserializeMvcNumericEnum(); }, Throws.InstanceOf<JsonException>());
            Assert.That(() => { deserializeHttpNumericEnum(); }, Throws.InstanceOf<JsonException>());
            Assert.That(persistedEnum, Is.EqualTo(JsonContractMode.ExactName));
        });
    }

    [Test]
    public async Task JwtBearerOptions_PreserveValidationAndHubQueryTokenBehavior()
    {
        var options = GetJwtBearerOptions();
        var validation = options.TokenValidationParameters;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(validation.ValidateIssuer, Is.False);
            Assert.That(validation.ValidateAudience, Is.False);
            Assert.That(validation.ValidateIssuerSigningKey, Is.True);
            Assert.That(validation.ClockSkew, Is.EqualTo(TimeSpan.Zero));
            var assertedValue7 = validation.IssuerSigningKey;
            Assert.That(assertedValue7, Is.TypeOf<SymmetricSecurityKey>());
            Assert.That(((SymmetricSecurityKey)assertedValue7).Key, Is.EqualTo(Encoding.UTF8.GetBytes(ApiHostFactory.JwtSigningKey)));
        }

        var hubHttpContext = CreateHttpContext("/hubs/notifications", "?access_token=hub-token");
        var hubContext = new MessageReceivedContext(hubHttpContext, BearerScheme, options);
        await options.Events.MessageReceived(hubContext);
        Assert.That(hubContext.Token, Is.EqualTo("hub-token"));

        var apiHttpContext = CreateHttpContext("/api/users", "?access_token=api-token");
        var apiContext = new MessageReceivedContext(apiHttpContext, BearerScheme, options);
        await options.Events.MessageReceived(apiContext);
        Assert.That(apiContext.Token, Is.Null);
    }

    [Test]
    public async Task JwtBearerEvents_PreserveLegacyErrorResponses()
    {
        var options = GetJwtBearerOptions();

        var expiredHttpContext = CreateHttpContext("/api/protected");
        var expiredContext = new AuthenticationFailedContext(expiredHttpContext, BearerScheme, options)
        {
            Exception = new SecurityTokenExpiredException()
        };
        await options.Events.AuthenticationFailed(expiredContext);
        await AssertErrorResponseAsync(expiredHttpContext, StatusCodes.Status401Unauthorized, Messages.ExpiredToken);

        var invalidHttpContext = CreateHttpContext("/api/protected");
        var invalidContext = new AuthenticationFailedContext(invalidHttpContext, BearerScheme, options)
        {
            Exception = new SecurityTokenInvalidSignatureException()
        };
        await options.Events.AuthenticationFailed(invalidContext);
        Assert.That(invalidHttpContext.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
        Assert.That(invalidHttpContext.Response.Body.Length, Is.EqualTo(0));

        var challengeHttpContext = CreateHttpContext("/api/protected");
        var challengeContext = new JwtBearerChallengeContext(
            challengeHttpContext,
            BearerScheme,
            options,
            new AuthenticationProperties());
        await options.Events.Challenge(challengeContext);
        Assert.That(challengeContext.Handled, Is.True);
        await AssertErrorResponseAsync(challengeHttpContext, StatusCodes.Status401Unauthorized, Messages.InvalidToken);

        var forbiddenHttpContext = CreateHttpContext("/api/protected");
        var forbiddenContext = new ForbiddenContext(forbiddenHttpContext, BearerScheme, options);
        await options.Events.Forbidden(forbiddenContext);
        await AssertErrorResponseAsync(forbiddenHttpContext, StatusCodes.Status403Forbidden, Messages.Unauthorized);
    }

    [Test]
    public async Task AuthorizationOptions_RegisterTheFivePermissionPolicies()
    {
        var policyProvider = _factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var missingPolicies = await FindMissingPoliciesAsync(policyProvider);

        Assert.That(missingPolicies, Is.Empty);
        Assert.That(ExpectedPolicies, Has.Exactly(5).Items);

        foreach (var (policyName, permission) in ExpectedPolicies)
        {
            var policy = await policyProvider.GetPolicyAsync(policyName);
            Assert.That(policy, Is.Not.Null);
            Assert.That(policy!.AuthenticationSchemes, Is.Empty);
            var assertedValue8 = (policy.Requirements
                            .OfType<CurrentPermissionRequirement>()).ToArray();
            Assert.That(assertedValue8, Has.Exactly(1).Items);
            var permissionRequirement = assertedValue8.Single();
            Assert.That(permissionRequirement.Permission, Is.EqualTo(permission));
        }
    }

    [Test]
    public async Task MissingPolicyFixture_IsDetected()
    {
        var services = new ServiceCollection();
        var authorization = services.AddAuthorizationBuilder();
        foreach (var (policyName, permission) in ExpectedPolicies
                     .Where(policy => policy.Key != AuthConstants.Policies.TrainerAccess))
        {
            authorization.AddPolicy(policyName, policy =>
                policy.RequireClaim(AuthConstants.PermissionClaimType, permission));
        }

        using var fixtureServices = services.BuildServiceProvider();
        var policyProvider = fixtureServices.GetRequiredService<IAuthorizationPolicyProvider>();

        var missingPolicies = await FindMissingPoliciesAsync(policyProvider);

        Assert.That(missingPolicies, Is.EqualTo(new[] { AuthConstants.Policies.TrainerAccess }));
    }

    [Test]
    public async Task RequestLocalization_PreservesEnglishDefaultAndAcceptLanguageOnlySelection()
    {
        var englishMessage = GetMessageForCulture("en", () => Messages.InvalidToken);
        var polishMessage = GetMessageForCulture("pl", () => Messages.InvalidToken);
        Assert.That(englishMessage, Is.Not.EqualTo(polishMessage));

        Assert.That((await GetChallengeMessageAsync()), Is.EqualTo(englishMessage));
        Assert.That((await GetChallengeMessageAsync(request => request.Headers.AcceptLanguage.ParseAdd("pl"))), Is.EqualTo(polishMessage));
        Assert.That((await GetChallengeMessageAsync(request => request.Headers.AcceptLanguage.ParseAdd("de"))), Is.EqualTo(englishMessage));
        Assert.That((await GetChallengeMessageAsync(request => request.RequestUri = new Uri("/api/admin/users/not-a-guid?culture=pl", UriKind.Relative))), Is.EqualTo(englishMessage));
        Assert.That((await GetChallengeMessageAsync(request => request.Headers.TryAddWithoutValidation(
                "Cookie",
                ".AspNetCore.Culture=c%3Dpl%7Cuic%3Dpl"))), Is.EqualTo(englishMessage));
    }

    private JwtBearerOptions GetJwtBearerOptions()
    {
        return _factory.Services
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
    }

    private DefaultHttpContext CreateHttpContext(string path, string? queryString = null)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = _factory.Services
        };
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(queryString ?? string.Empty);
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task AssertErrorResponseAsync(HttpContext context, int statusCode, string message)
    {
        Assert.That(context.Response.StatusCode, Is.EqualTo(statusCode));
        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.That(body.RootElement.EnumerateObject().Select(property => property.Name), Is.EqualTo(new[] { "msg" }));
        Assert.That(body.RootElement.GetProperty("msg").GetString(), Is.EqualTo(message));
    }

    private static async Task<IReadOnlyList<string>> FindMissingPoliciesAsync(IAuthorizationPolicyProvider policyProvider)
    {
        var missing = new List<string>();
        foreach (var policyName in ExpectedPolicies.Keys)
        {
            if (await policyProvider.GetPolicyAsync(policyName) == null)
            {
                missing.Add(policyName);
            }
        }

        return missing;
    }

    private async Task<string> GetChallengeMessageAsync(Action<HttpRequestMessage>? configure = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/users/not-a-guid");
        configure?.Invoke(request);
        using var response = await _client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("msg").GetString()!;
    }

    private static string GetMessageForCulture(string cultureName, Func<string> messageAccessor)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            return messageAccessor();
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private static AuthenticationScheme BearerScheme { get; } = new(
        JwtBearerDefaults.AuthenticationScheme,
        displayName: null,
        typeof(JwtBearerHandler));

    private sealed class ApiHostFactory : WebApplicationFactory<Program>
    {
        internal const string JwtSigningKey = "ApiHostCharacterizationSigningKey_AtLeast32Characters!";

        private readonly InMemoryDatabaseRoot _databaseRoot = new();
        private readonly string _databaseName = $"api_host_characterization_{Id<ApiHostFactory>.New():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                var descriptorsToRemove = services
                    .Where(descriptor => descriptor.ServiceType == typeof(DbContextOptions<AppDbContext>)
                        || descriptor.ServiceType == typeof(AppDbContext)
                        || descriptor.ServiceType.FullName?.Contains("EntityFrameworkCore", StringComparison.Ordinal) == true)
                    .ToList();

                foreach (var descriptor in descriptorsToRemove)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName, _databaseRoot));
            });

            builder.UseSetting("Jwt:SigningKey", JwtSigningKey);
            builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost");
            builder.UseSetting("Email:Enabled", "false");
        }
    }

    private sealed class JsonContractEntity;

    private sealed record JsonContractSample(
        string DisplayName,
        string? OmittedValue,
        IReadOnlyDictionary<string, string> Lookup,
        Id<JsonContractEntity> EntityId,
        JsonContractMode Mode);

    private enum JsonContractMode
    {
        ExactName
    }
}
