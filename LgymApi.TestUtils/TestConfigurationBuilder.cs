using Microsoft.Extensions.Configuration;

namespace LgymApi.TestUtils;

public static class TestConfigurationBuilder
{
    public static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    public static IConfiguration BuildEnabledEmailConfiguration()
    {
        return BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test",
            ["Email:Enabled"] = "true",
            ["Email:InvitationBaseUrl"] = "https://example.com/invite",
            ["Email:PasswordRecoveryBaseUrl"] = "https://example.com/reset",
            ["Email:TemplateRootPath"] = "EmailTemplates",
            ["Email:DefaultCulture"] = "en-US",
            ["Email:FromAddress"] = "coach@example.com",
            ["Email:SmtpHost"] = "smtp.example.com",
            ["Email:SmtpPort"] = "587"
        });
    }

    public static Dictionary<string, string?> ToDictionary(IConfiguration configuration)
    {
        return configuration.AsEnumerable().ToDictionary(x => x.Key, x => x.Value);
    }
}
