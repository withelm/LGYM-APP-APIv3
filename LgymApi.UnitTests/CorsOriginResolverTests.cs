using LgymApi.Api.Configuration;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class CorsOriginResolverTests
{
    [Test]
    public void ResolveAllowedOrigins_InDevelopmentWithoutConfiguredOrigins_ReturnsFallbackOrigins()
    {
        var result = CorsOriginResolver.ResolveAllowedOrigins(null, isDevelopment: true);

        Assert.That(result, Is.EqualTo(new[]
        {
            "http://localhost:3000",
            "http://127.0.0.1:3000",
            "http://localhost:5173",
            "http://127.0.0.1:5173"
        }));
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

        Assert.That(result, Is.EqualTo(new[] { "https://app.example.com", "https://admin.example.com" }));
    }
}
