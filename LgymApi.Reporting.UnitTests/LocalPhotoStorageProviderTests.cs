using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LgymApi.Application.Options;
using LgymApi.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class LocalPhotoStorageProviderTests
{
    private const string TestSigningKey = "task6-local-photo-test-key-32-bytes-minimum";
    private static readonly DateTimeOffset FixedUtcNow = DateTimeOffset.FromUnixTimeSeconds(2_000_000_000);

    [Test]
    [Category("Task6Baseline")]
    public async Task GenerateSignedUploadUrlAsync_WhenRequestHostAvailable_UsesCurrentHost()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("api.example.com");
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        var provider = new LocalPhotoStorageProvider(
            accessor,
            new LocalPhotoDevelopmentStore(),
            new LocalPhotoDevelopmentUrlSigner(TestSigningKey, TimeProvider.System));

        var result = await provider.GenerateSignedUploadUrlAsync("photos/key one.jpg", "image/jpeg", TimeSpan.FromMinutes(10));

        Assert.That(result, Does.StartWith("https://api.example.com/dev/photos/upload/"));
        Assert.That(result, Does.Contain("photos%2Fkey%20one.jpg"));

        var uri = new Uri(result);
        var query = QueryHelpers.ParseQuery(uri.Query);
        Assert.That(query, Has.Exactly(3).Items);
        Assert.That(query, Does.ContainKey("v"));
        Assert.That(query["v"].ToArray(), Is.EqualTo(new[] { "1" }));
        Assert.That(query, Does.ContainKey("expires"));
        Assert.That(long.TryParse(query["expires"].Single(), out var expiresAt), Is.True);
        Assert.That(expiresAt, Is.GreaterThan(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        Assert.That(query, Does.ContainKey("sig"));
        Assert.That(query["sig"], Has.Exactly(1).Items);
    }

    [Test]
    [Category("Task6Baseline")]
    public async Task GenerateSignedReadUrlAsync_WhenRequestMissing_UsesFallbackLocalhost()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);
        var provider = new LocalPhotoStorageProvider(
            accessor,
            new LocalPhotoDevelopmentStore(),
            new LocalPhotoDevelopmentUrlSigner(TestSigningKey, TimeProvider.System));

        var result = await provider.GenerateSignedReadUrlAsync("photos/key.jpg", TimeSpan.FromMinutes(5));

        Assert.That(result, Does.StartWith("https://localhost:7025/dev/photos/read/"));
    }

    [Test]
    [Category("Task6Baseline")]
    public void CanonicalHmacSha256Oracle_WhenKnownTupleIsSigned_MatchesKnownVector()
    {
        var signature = CalculateExpectedSignature(
            "GET",
            "photos/reports/key one.jpg",
            2_000_000_300);

        Assert.That(signature, Is.EqualTo("tmnfGtF-7OmRw3iNHw6nJ29BUNphS2DJjdGgE9ZeVh0"));
    }

    [TestCase(true, "PUT", "/dev/photos/upload/")]
    [TestCase(false, "GET", "/dev/photos/read/")]
    [Category("Task6Red")]
    public async Task GeneratedUrl_WhenLocalCapabilityIsIssued_ContainsCanonicalHmacSha256Fields(
        bool upload,
        string expectedMethod,
        string expectedPathPrefix)
    {
        await using var services = CreateConfiguredServices();
        var provider = ActivatorUtilities.CreateInstance<LocalPhotoStorageProvider>(services);
        const string normalizedStorageKey = "photos/reports/key one.jpg";
        var expectedExpiry = FixedUtcNow.AddMinutes(5).ToUnixTimeSeconds();
        var expectedSignature = CalculateExpectedSignature(expectedMethod, normalizedStorageKey, expectedExpiry);

        var result = upload
            ? await provider.GenerateSignedUploadUrlAsync(normalizedStorageKey, "image/jpeg", TimeSpan.FromMinutes(5))
            : await provider.GenerateSignedReadUrlAsync(normalizedStorageKey, TimeSpan.FromMinutes(5));

        var uri = new Uri(result);
        var query = QueryHelpers.ParseQuery(uri.Query);
        Assert.That(query, Does.ContainKey("v"));
        Assert.That(query["v"].ToArray(), Is.EqualTo(new[] { "1" }));
        Assert.That(query, Does.ContainKey("expires"));
        Assert.That(query["expires"], Has.Exactly(1).Items);
        Assert.That(long.TryParse(query["expires"].Single(), out var expiresAt), Is.True);
        Assert.That(expiresAt, Is.EqualTo(expectedExpiry));
        Assert.That(query, Does.ContainKey("sig"));
        Assert.That(query["sig"], Has.Exactly(1).Items);
        Assert.That(query["sig"].Single(), Is.EqualTo(expectedSignature));
        Assert.That(uri.AbsolutePath, Is.EqualTo($"{expectedPathPrefix}{Uri.EscapeDataString(normalizedStorageKey)}"));
    }

    private static ServiceProvider CreateConfiguredServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PhotoStorage:LocalDevelopmentSigningKey"] = TestSigningKey
            })
            .Build();
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("localhost:7025");

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = context });
        services.AddSingleton(new LocalPhotoDevelopmentStore());
        var timeProvider = new FixedTimeProvider(FixedUtcNow);
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddSingleton(new LocalPhotoDevelopmentUrlSigner(TestSigningKey, timeProvider));
        services.Configure<PhotoStorageOptions>(configuration.GetSection("PhotoStorage"));
        return services.BuildServiceProvider();
    }

    private static string CalculateExpectedSignature(string method, string normalizedStorageKey, long expiresAt)
    {
        var canonicalTuple = string.Join('\n', "1", method, normalizedStorageKey, expiresAt.ToString(CultureInfo.InvariantCulture));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(TestSigningKey));
        return WebEncoders.Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonicalTuple)));
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
