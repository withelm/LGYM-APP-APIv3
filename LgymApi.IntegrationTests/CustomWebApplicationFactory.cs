using LgymApi.Infrastructure.Data;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.Domain.ValueObjects;
using LgymApi.TestUtils;
using LgymApi.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LgymApi.IntegrationTests;

/// <summary>
/// Configure the web application host for integration testing with in-memory database and test email sender.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Gets a unique database name for this test instance.
    /// </summary>
    public string DatabaseName { get; } = $"lgymtests_{Id<CustomWebApplicationFactory>.New():N}";
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
        builder.ConfigureServices(services =>
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

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);

            services.RemoveAll<IHangfireJobReconciler>();
            services.AddSingleton<InMemoryHangfireJobReconciler>();
            services.AddSingleton<IHangfireJobReconciler>(sp => sp.GetRequiredService<InMemoryHangfireJobReconciler>());

            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
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
}
