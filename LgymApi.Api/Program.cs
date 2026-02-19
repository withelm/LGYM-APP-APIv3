using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using FluentValidation.AspNetCore;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application;
using LgymApi.Infrastructure;
using LgymApi.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.OpenApi;
using LgymApi.Api.Middleware;
using LgymApi.Domain.Enums;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.UseInlineDefinitionsForEnums();
    options.MapType<Platforms>(() => new OpenApiSchema
    {
        Type = JsonSchemaType.String,
        Enum = Enum.GetNames<Platforms>().Select(x => (JsonNode?)JsonValue.Create(x)).ToList()
    });
    options.MapType<BodyParts>(() => new OpenApiSchema
    {
        Type = JsonSchemaType.String,
        Enum = Enum.GetNames<BodyParts>().Select(x => (JsonNode?)JsonValue.Create(x)).ToList()
    });
    options.MapType<WeightUnits>(() => new OpenApiSchema
    {
        Type = JsonSchemaType.String,
        Enum = Enum.GetNames<WeightUnits>().Select(x => (JsonNode?)JsonValue.Create(x)).ToList()
    });
    options.MapType<HeightUnits>(() => new OpenApiSchema
    {
        Type = JsonSchemaType.String,
        Enum = Enum.GetNames<HeightUnits>().Select(x => (JsonNode?)JsonValue.Create(x)).ToList()
    });
});
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddLocalization();
builder.Services.AddApplicationMapping(typeof(Program).Assembly, typeof(IMappingProfile).Assembly);
builder.Services.AddApplicationServices();

builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());

var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
{
    throw new InvalidOperationException("Jwt:Secret is not configured or is too short. Set a strong secret value.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
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

builder.Services.AddAuthorization();

if (!builder.Environment.IsEnvironment("Testing"))
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

app.UseRequestLocalization(localizationOptions);
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseRateLimiter();
}

app.UseMiddleware<LgymApi.Api.Middleware.UserContextMiddleware>();

app.MapControllers();

app.Run();

// For WebApplicationFactory in integration tests
public partial class Program { }
