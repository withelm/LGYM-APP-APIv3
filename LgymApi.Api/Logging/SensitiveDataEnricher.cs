using Serilog.Core;
using Serilog.Events;

namespace LgymApi.Api.Logging;

/// <summary>
/// Masks sensitive property values (passwords, tokens, secrets, connection
/// strings, signing keys, emails, JWTs) as "***" before they leave the app.
/// Never throws and never drops the whole event.
/// </summary>
public sealed class SensitiveDataEnricher : ILogEventEnricher
{
    private static readonly string[] SensitiveKeys =
    {
        "password", "pass", "pwd", "token", "accesstoken", "refreshtoken",
        "secret", "apikey", "api_key", "connectionstring", "connectionstrings",
        "signingkey", "authorization", "credential", "credentials"
    };

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        try
        {
            foreach (var key in logEvent.Properties
                         .Where(property => IsSensitiveKey(property.Key) || HasSensitiveValue(property.Value))
                         .Select(property => property.Key)
                         .ToArray())
            {
                logEvent.RemovePropertyIfPresent(key);
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(key, "***"));
            }
        }
        catch
        {
            // Never break logging because of redaction.
        }
    }

    private static bool IsSensitiveKey(string key)
        => SensitiveKeys.Any(s => key.Contains(s, StringComparison.OrdinalIgnoreCase));

    private static bool HasSensitiveValue(LogEventPropertyValue value)
        => value is ScalarValue { Value: string str } && LooksLikeSecret(str);

    private static bool LooksLikeSecret(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // Rough email: contains '@' and a dot after it.
        var atIndex = value.IndexOf('@');
        if (atIndex >= 0 && value.IndexOf('.', atIndex + 1) >= 0)
        {
            return true;
        }

        // Rough JWT: 3 dot-separated base64url segments, fairly long.
        var parts = value.Split('.');
        return parts.Length == 3 && value.Length > 30;
    }
}
