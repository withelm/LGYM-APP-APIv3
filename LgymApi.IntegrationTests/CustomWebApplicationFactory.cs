using LgymApi.Infrastructure.Data;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.Domain.ValueObjects;
using LgymApi.TestUtils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace LgymApi.IntegrationTests;

/// <summary>
/// Configure the web application host for integration testing with an in-memory database by default,
/// or a dedicated Postgres database when the DB-backed CI mode is enabled.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string DefaultPostgresConnectionString = "Host=localhost;Port=5433;Database=LGYM-APP;Username=postgres;Password=REPLACE_ME;TimeZone=Europe/Warsaw";

    /// <summary>
    /// Gets a unique database name for this test instance.
    /// </summary>
    public string DatabaseName { get; } = $"lgymtests_{Id<CustomWebApplicationFactory>.New():N}";

    /// <summary>
    /// Gets whether this test host should connect to a dedicated Postgres database.
    /// </summary>
    public bool UsesPostgresDatabase { get; } = UsePostgresIntegrationDatabase();

    /// <summary>
    /// Gets the test email sender instance for capturing outbound emails during tests.
    /// </summary>
    public TestEmailSender EmailSender { get; } = new();

    /// <summary>
    /// The JWT signing key used for generating test tokens during integration tests.
    /// </summary>
    public const string TestJwtSigningKey = "IntegrationTestSigningKey_MustBeAtLeast32Characters!";

    /// <summary>
    /// Configure the test web host with in-memory database, JWT settings, email configuration, and CORS policies.
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        if (UsesPostgresDatabase)
        {
            var postgresConnectionString = BuildTestPostgresConnectionString();
            CreateDatabase(postgresConnectionString);
            builder.UseSetting("ConnectionStrings:Postgres", postgresConnectionString);
        }

        builder.ConfigureServices(services =>
        {
            if (!UsesPostgresDatabase)
            {
                var descriptorsToRemove = services
                    .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                             || d.ServiceType == typeof(AppDbContext)
                             || d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true)
                    .ToList();

                foreach (var descriptor in descriptorsToRemove)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseInMemoryDatabase(DatabaseName);
                    options.EnableSensitiveDataLogging();
                });
            }

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);

            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (UsesPostgresDatabase)
            {
                db.Database.Migrate();
            }
            else
            {
                db.Database.EnsureCreated();
            }

            TestDataFactory.SeedDefaultRolesAsync(db).GetAwaiter().GetResult();
            db.SaveChanges();
        });

        builder.UseSetting("Jwt:SigningKey", TestJwtSigningKey);
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost");
        builder.UseSetting("Email:Enabled", "true");
        builder.UseSetting("Email:FromAddress", "no-reply@test.local");
        builder.UseSetting("Email:SmtpHost", "localhost");
        builder.UseSetting("Email:SmtpPort", "1025");
        builder.UseSetting("Email:InvitationBaseUrl", "https://app.test.local/invitations");
        builder.UseSetting("Email:TemplateRootPath", Path.Combine(AppContext.BaseDirectory, "EmailTemplates"));
        builder.UseSetting("Email:DefaultCulture", "en-US");
    }

    public void DropPostgresDatabase()
    {
        if (!UsesPostgresDatabase)
        {
            return;
        }

        var connectionString = BuildAdminPostgresConnectionString();
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"""DROP DATABASE IF EXISTS "{EscapeIdentifier(DatabaseName)}" WITH (FORCE);""";
        command.ExecuteNonQuery();
    }

    private string BuildTestPostgresConnectionString()
    {
        var builder = new NpgsqlConnectionStringBuilder(GetBasePostgresConnectionString())
        {
            Database = DatabaseName,
            Pooling = false
        };

        return builder.ConnectionString;
    }

    private string BuildAdminPostgresConnectionString()
    {
        var builder = new NpgsqlConnectionStringBuilder(GetBasePostgresConnectionString())
        {
            Database = "postgres",
            Pooling = false
        };

        return builder.ConnectionString;
    }

    private static string GetBasePostgresConnectionString()
        => Environment.GetEnvironmentVariable("ConnectionStrings__Postgres") ?? DefaultPostgresConnectionString;

    private void CreateDatabase(string postgresConnectionString)
    {
        var adminConnectionString = new NpgsqlConnectionStringBuilder(postgresConnectionString)
        {
            Database = "postgres",
            Pooling = false
        }.ConnectionString;

        using var connection = new NpgsqlConnection(adminConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"""CREATE DATABASE "{EscapeIdentifier(DatabaseName)}";""";
        command.ExecuteNonQuery();
    }

    private static bool UsePostgresIntegrationDatabase()
        => string.Equals(
            Environment.GetEnvironmentVariable("LGYM_INTEGRATION_DB_PROVIDER"),
            "Postgres",
            StringComparison.OrdinalIgnoreCase);

    private static string EscapeIdentifier(string identifier)
        => identifier.Replace("\"", "\"\"");
}
