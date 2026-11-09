using FluentAssertions;
using LgymApi.Infrastructure.Options;
using LgymApi.Infrastructure.Services;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ExerciseMediaStorageOptionsTests
{
    private static ExerciseMediaStorageOptions Options() => new() { Enabled = true, Endpoint = "https://technical-account.r2.cloudflarestorage.com", AccessKeyId = "synthetic-exercise-access", SecretAccessKey = "synthetic-exercise-secret", PrivateBucket = "exercise-private-staging", PublishedBucket = "exercise-published-staging", PublicBaseUrl = "https://exercise-staging.example.invalid" };

    [Test]
    public void DedicatedOriginsAndSeparateBuckets_AreRequired()
    {
        var options = Options(); options.Validate();
        options.PublishedBucket = options.PrivateBucket;
        Action invalid = options.Validate; invalid.Should().Throw<InvalidOperationException>();
        options = Options(); options.PublicBaseUrl = "https://exercise.r2.dev"; invalid = options.Validate; invalid.Should().Throw<InvalidOperationException>();
        options = Options(); options.PublicBaseUrl = "http://exercise.example.invalid"; invalid = options.Validate; invalid.Should().Throw<InvalidOperationException>();
        options = Options(); options.Endpoint = "https://unexpected.example.invalid"; invalid = options.Validate; invalid.Should().Throw<InvalidOperationException>();
        options = Options(); options.Endpoint += "/?secret=unexpected"; invalid = options.Validate; invalid.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public async Task SignedPut_UsesPrivateBucketContentTypeAndShortExpirationWithoutSecret()
    {
        using var storage = new ExerciseMediaStorage(Options());
        var expiry = DateTimeOffset.UtcNow.AddMinutes(5);
        var url = await storage.SignUploadAsync("staging/00000000-0000-0000-0000-000000000001/source", "image/gif", expiry, default);
        var uri = new Uri(url);
        uri.Host.Should().Be("technical-account.r2.cloudflarestorage.com");
        uri.AbsolutePath.Should().StartWith("/exercise-private-staging/staging/");
        url.Should().Contain("X-Amz-Signature=").And.NotContain("synthetic-exercise-secret");
        uri.Query.Should().Contain("X-Amz-Expires=");
    }
}
