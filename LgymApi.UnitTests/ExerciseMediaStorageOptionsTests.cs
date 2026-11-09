using FluentAssertions;
using LgymApi.Infrastructure.Options;
using LgymApi.Infrastructure.Services;
using LgymApi.Infrastructure;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.TestUtils;
using Microsoft.Extensions.DependencyInjection;

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

    [TestCase("not an absolute URI")]
    [TestCase("http://technical-account.r2.cloudflarestorage.com")]
    [TestCase("https://technical-account.r2.cloudflarestorage.com/private")]
    [TestCase("https://user@technical-account.r2.cloudflarestorage.com")]
    [TestCase("https://technical-account.r2.cloudflarestorage.com/#fragment")]
    [TestCase("https://technical-account.r2.cloudflarestorage.com:8443")]
    public void R2Endpoint_WithUnsupportedAuthorityOrPrivatePath_IsRejected(string endpoint)
    {
        var options = Options();
        options.Endpoint = endpoint;
        Action validate = options.Validate;
        validate.Should().Throw<InvalidOperationException>();
    }

    [TestCase("not an absolute URI")]
    [TestCase("https://exercise.example.invalid/path")]
    [TestCase("https://exercise.example.invalid/?signature=private")]
    [TestCase("https://exercise.example.invalid/#fragment")]
    [TestCase("https://user@exercise.example.invalid")]
    [TestCase("https://exercise.example.invalid:8443")]
    [TestCase("https://technical-account.r2.cloudflarestorage.com")]
    public void PublicOrigin_WithPrivateAuthorityOrNonOriginComponents_IsRejected(string origin)
    {
        var options = Options();
        options.PublicBaseUrl = origin;
        Action validate = options.Validate;
        validate.Should().Throw<InvalidOperationException>();
    }

    [TestCase("PrivateBucket")]
    [TestCase("PublishedBucket")]
    [TestCase("AccessKeyId")]
    [TestCase("SecretAccessKey")]
    public void MissingDedicatedStorageSetting_FailsClosed(string setting)
    {
        var options = Options();
        switch (setting)
        {
            case "PrivateBucket": options.PrivateBucket = " "; break;
            case "PublishedBucket": options.PublishedBucket = " "; break;
            case "AccessKeyId": options.AccessKeyId = " "; break;
            case "SecretAccessKey": options.SecretAccessKey = " "; break;
        }
        Action validate = options.Validate;
        validate.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void DisabledStorage_DoesNotRequireCredentialsOrPermitSigning()
    {
        var options = new ExerciseMediaStorageOptions();
        options.Validate();
        using var storage = new ExerciseMediaStorage(options);
        storage.IsConfigured.Should().BeFalse();
        Action sign = () => storage.SignUploadAsync("staging/fixture/source", "image/gif", DateTimeOffset.UtcNow, default);
        sign.Should().Throw<InvalidOperationException>();
    }

    [TestCase("private")]
    [TestCase("published")]
    [TestCase("credentials")]
    public void InfrastructureRegistration_RejectsSharedPrivateReportAuthority(string sharedAuthority)
    {
        var values = DedicatedConfiguration();
        if (sharedAuthority == "private") values["PhotoStorage:BucketName"] = values["ExerciseMediaStorage:PrivateBucket"];
        if (sharedAuthority == "published") values["PhotoStorage:BucketName"] = values["ExerciseMediaStorage:PublishedBucket"];
        if (sharedAuthority == "credentials") values["PhotoStorage:AccessKeyId"] = values["ExerciseMediaStorage:AccessKeyId"];
        var services = new ServiceCollection();
        Action register = () => services.AddInfrastructure(TestConfigurationBuilder.BuildConfiguration(values), enableSensitiveLogging: false, isTesting: true);
        register.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void InfrastructureRegistration_AcceptsDedicatedExerciseAuthority()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(TestConfigurationBuilder.BuildConfiguration(DedicatedConfiguration()), enableSensitiveLogging: false, isTesting: true);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExerciseMediaStorage>().IsConfigured.Should().BeTrue();
    }

    private static Dictionary<string, string?> DedicatedConfiguration()
    {
        var values = TestConfigurationBuilder.ToDictionary(CompositionRootTestHost.CreateConfiguration());
        values["ExerciseMediaStorage:Enabled"] = "true";
        values["ExerciseMediaStorage:Endpoint"] = "https://technical-account.r2.cloudflarestorage.com";
        values["ExerciseMediaStorage:AccessKeyId"] = "synthetic-exercise-access";
        values["ExerciseMediaStorage:SecretAccessKey"] = "synthetic-exercise-secret";
        values["ExerciseMediaStorage:PrivateBucket"] = "exercise-private-staging";
        values["ExerciseMediaStorage:PublishedBucket"] = "exercise-published-staging";
        values["ExerciseMediaStorage:PublicBaseUrl"] = "https://exercise-staging.example.invalid";
        values["PhotoStorage:BucketName"] = "report-private-staging";
        values["PhotoStorage:AccessKeyId"] = "synthetic-report-access";
        return values;
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
