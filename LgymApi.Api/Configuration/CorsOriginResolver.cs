namespace LgymApi.Api.Configuration;

public static class CorsOriginResolver
{
    private static readonly string[] DevelopmentFallbackOrigins =
    [
        "http://localhost:3000",
        "http://127.0.0.1:3000",
        "http://localhost:5173",
        "http://127.0.0.1:5173"
    ];

    public static string[] ResolveAllowedOrigins(IEnumerable<string>? configuredOrigins, bool isDevelopment)
    {
        var normalizedOrigins = configuredOrigins?
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        if (normalizedOrigins.Length > 0)
        {
            return normalizedOrigins;
        }

        return isDevelopment ? DevelopmentFallbackOrigins : [];
    }
}
