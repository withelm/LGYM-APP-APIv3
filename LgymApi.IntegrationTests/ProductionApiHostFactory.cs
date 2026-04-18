using System.Linq;
using LgymApi.TestUtils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace LgymApi.IntegrationTests;

public sealed class ProductionApiHostFactory : WebApplicationFactory<Program>
{
    private readonly string? _previousJwtSigningKey = Environment.GetEnvironmentVariable("Jwt__SigningKey");

    public ProductionApiHostFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__SigningKey", CustomWebApplicationFactory.TestJwtSigningKey);
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", "Host=192.168.1.12;Port=5444;Database=Lgym3;Username=admin;Password=SuperHaslo123!;TimeZone=Europe/Warsaw");
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", "http://localhost");
        Environment.SetEnvironmentVariable("Email__Enabled", "true");
        Environment.SetEnvironmentVariable("Email__FromAddress", "no-reply@test.local");
        Environment.SetEnvironmentVariable("Email__SmtpHost", "localhost");
        Environment.SetEnvironmentVariable("Email__SmtpPort", "1025");
        Environment.SetEnvironmentVariable("Email__InvitationBaseUrl", "https://app.test.local/invitations");
        Environment.SetEnvironmentVariable("Email__TemplateRootPath", Path.Combine(AppContext.BaseDirectory, "EmailTemplates"));
        Environment.SetEnvironmentVariable("Email__DefaultCulture", "en-US");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services
                         .Where(d => d.ServiceType == typeof(IHostedService))
                         .ToList())
            {
                services.Remove(descriptor);
            }
        });
    }

    public new void Dispose()
    {
        Environment.SetEnvironmentVariable("Jwt__SigningKey", _previousJwtSigningKey);
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", null);
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", null);
        Environment.SetEnvironmentVariable("Email__Enabled", null);
        Environment.SetEnvironmentVariable("Email__FromAddress", null);
        Environment.SetEnvironmentVariable("Email__SmtpHost", null);
        Environment.SetEnvironmentVariable("Email__SmtpPort", null);
        Environment.SetEnvironmentVariable("Email__InvitationBaseUrl", null);
        Environment.SetEnvironmentVariable("Email__TemplateRootPath", null);
        Environment.SetEnvironmentVariable("Email__DefaultCulture", null);
        base.Dispose();
    }
}
