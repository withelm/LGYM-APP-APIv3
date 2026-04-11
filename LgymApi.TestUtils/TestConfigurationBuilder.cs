using Microsoft.Extensions.Configuration;

namespace LgymApi.TestUtils;

/// <summary>
/// Builds IConfiguration instances with preset or custom key-value pairs for test scenarios.
/// </summary>
public static class TestConfigurationBuilder
{
    /// <summary>
    /// Builds an in-memory configuration from the provided dictionary of key-value pairs.
    /// </summary>
    public static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    /// <summary>
    /// Builds a configuration with Email:Enabled=true and all required SMTP settings for testing email flows.
    /// </summary>
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

    /// <summary>
    /// Flattens an IConfiguration instance into a dictionary of all keys and values.
    /// </summary>
    public static Dictionary<string, string?> ToDictionary(IConfiguration configuration)
    {
        return configuration.AsEnumerable().ToDictionary(x => x.Key, x => x.Value);
    }
}
