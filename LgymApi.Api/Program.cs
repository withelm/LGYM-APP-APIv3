using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using FluentValidation.AspNetCore;
using LgymApi.BackgroundWorker;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application;
using LgymApi.Infrastructure;
using LgymApi.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using LgymApi.Api.Configuration;
using Microsoft.AspNetCore.Localization;
using LgymApi.Api.Middleware;
using LgymApi.Domain.Security;
using Hangfire;
using LgymApi.BackgroundWorker.Common.Outbox;
using LgymApi.Api.Serialization;

const string TestingEnvironment = "Testing";

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
    });

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.UseInlineDefinitionsForEnums();
    options.SchemaFilter<EnumAsStringSchemaFilter>();
});
var configuredCorsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
var corsAllowedOrigins = CorsOriginResolver.ResolveAllowedOrigins(configuredCorsOrigins, builder.Environment.IsDevelopment());

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (corsAllowedOrigins.Length > 0)
        {
            policy.WithOrigins(corsAllowedOrigins).AllowAnyMethod().AllowAnyHeader();
            return;
        }

        throw new InvalidOperationException("No CORS allowed origins are configured. Configure 'Cors:AllowedOrigins' or disable CORS explicitly.");
    });
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddLocalization();
builder.Services.AddApplicationMapping(typeof(Program).Assembly, typeof(IMappingProfile).Assembly);
builder.Services.AddApplicationServices();

builder.Services.AddInfrastructure(
    builder.Configuration,
    builder.Environment.IsDevelopment(),
    builder.Environment.IsEnvironment(TestingEnvironment),
    hostBackgroundServer: true);
builder.Services.AddBackgroundWorkerServices(builder.Environment.IsEnvironment(TestingEnvironment));

var jwtSigningKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(jwtSigningKey) || jwtSigningKey.Length < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey is not configured or is too short. Set a strong key value.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                if (context.Exception is SecurityTokenExpiredException)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return context.Response.WriteAsJsonAsync(new { message = Messages.ExpiredToken });
                }

                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                if (!context.Response.HasStarted)
                {
                    context.HandleResponse();
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return context.Response.WriteAsJsonAsync(new { message = Messages.InvalidToken });
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services
    .AddAuthorizationBuilder()
    .AddPolicy(AuthConstants.Policies.ManageUserRoles, policy =>
        policy.RequireClaim(AuthConstants.PermissionClaimType, AuthConstants.Permissions.ManageUserRoles))
    .AddPolicy(AuthConstants.Policies.ManageAppConfig, policy =>
        policy.RequireClaim(AuthConstants.PermissionClaimType, AuthConstants.Permissions.ManageAppConfig))
    .AddPolicy(AuthConstants.Policies.ManageGlobalExercises, policy =>
        policy.RequireClaim(AuthConstants.PermissionClaimType, AuthConstants.Permissions.ManageGlobalExercises))
    .AddPolicy(AuthConstants.Policies.TrainerAccess, policy =>
        policy.RequireRole(AuthConstants.Roles.Trainer));

if (!builder.Environment.IsEnvironment(TestingEnvironment))
{
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var path = context.Request.Path.Value ?? string.Empty;
            var isAuth = path.Contains("/login", StringComparison.OrdinalIgnoreCase)
                         || path.Contains("/register", StringComparison.OrdinalIgnoreCase);

            if (isAuth)
            {
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                {
                    // Intentional: allow higher burst for auth retries from mobile networks/devices.
                    PermitLimit = 200,
                    Window = TimeSpan.FromMinutes(15)
                });
            }

            var userId = context.User.FindFirst("userId")?.Value;
            var key = string.IsNullOrWhiteSpace(userId)
                ? context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
                : userId;

            return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                // Intentional: raise global throughput limit to reduce throttling in normal app usage.
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            });
        });
    });
}

var supportedCultures = new[]
{
    new CultureInfo("en"),
    new CultureInfo("pl")
};

var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
};

localizationOptions.RequestCultureProviders = new List<IRequestCultureProvider>
{
    new AcceptLanguageHeaderRequestCultureProvider()
};

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsEnvironment(TestingEnvironment))
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new HangfireDashboardAuthorizationFilter() }
    });

    RecurringJob.AddOrUpdate<IOutboxDispatcherJob>(
        "outbox-dispatcher",
        job => job.ExecuteAsync(CancellationToken.None),
        Cron.Minutely());
}

app.UseRequestLocalization(localizationOptions);
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
if (!app.Environment.IsEnvironment(TestingEnvironment))
{
    app.UseRateLimiter();
}

app.UseMiddleware<LgymApi.Api.Middleware.UserContextMiddleware>();

app.MapControllers();

await app.RunAsync();

// For WebApplicationFactory in integration tests
public partial class Program { }
