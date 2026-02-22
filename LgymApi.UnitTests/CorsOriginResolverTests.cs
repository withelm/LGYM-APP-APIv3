using LgymApi.Api.Configuration;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class CorsOriginResolverTests
{
    private static readonly string[] ExpectedDevelopmentOrigins =
    [
        "http://localhost:3000",
        "http://127.0.0.1:3000",
        "http://localhost:5173",
        "http://127.0.0.1:5173"
    ];

    private static readonly string[] ExpectedNormalizedConfiguredOrigins =
    [
        "https://app.example.com",
        "https://admin.example.com"
    ];

    [Test]
    public void ResolveAllowedOrigins_InDevelopmentWithoutConfiguredOrigins_ReturnsFallbackOrigins()
    {
        var result = CorsOriginResolver.ResolveAllowedOrigins(null, isDevelopment: true);

        Assert.That(result, Is.EqualTo(ExpectedDevelopmentOrigins));
    }

    [Test]
    public void ResolveAllowedOrigins_InProductionWithoutConfiguredOrigins_ReturnsEmpty()
    {
        var result = CorsOriginResolver.ResolveAllowedOrigins(Array.Empty<string>(), isDevelopment: false);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void ResolveAllowedOrigins_TrimsAndDeduplicatesConfiguredOrigins()
    {
        var configuredOrigins = new[]
        {
            " https://app.example.com ",
            "https://app.example.com",
            "",
            "   ",
            "https://admin.example.com"
        };

        var result = CorsOriginResolver.ResolveAllowedOrigins(configuredOrigins, isDevelopment: true);

        Assert.That(result, Is.EqualTo(ExpectedNormalizedConfiguredOrigins));
    }
}
