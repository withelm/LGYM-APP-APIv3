using FluentAssertions;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.Infrastructure;
using LgymApi.Infrastructure.Options;
using LgymApi.Infrastructure.Services;
using LgymApi.TestUtils;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ExerciseMediaStorageOptionsTests
{
    private static ExerciseMediaStorageOptions Options() => new()
    {
        Enabled = true,
        Endpoint = "https://technical-account.r2.cloudflarestorage.com",
        AccessKeyId = "synthetic-exercise-access",
        SecretAccessKey = "synthetic-exercise-secret",
        Bucket = "tenant-one-assets",
        PublicBaseUrl = "https://exercises-dev.owner.test"
    };

    [TestCase("tenant-one-assets")]
    [TestCase("tenant-two-images")]
    public void ArbitraryValidBucket_WithDedicatedOwnedOrigin_IsAccepted(string bucket)
    {
        var options = Options();
        options.Bucket = bucket;
        options.Validate();
    }

    [TestCase("")]
    [TestCase(" ")]
    [TestCase("ab")]
    [TestCase("-first")]
    [TestCase("last-")]
    [TestCase("contains.dot")]
    [TestCase("contains_underscore")]
    [TestCase("UPPERCASE")]
    [TestCase("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void InvalidR2BucketSyntax_IsRejected(string bucket)
    {
        var options = Options();
        options.Bucket = bucket;
        Action validate = options.Validate;
        validate.Should().Throw<InvalidOperationException>();
    }

    [TestCase("not an absolute URI")]
    [TestCase("http://technical-account.r2.cloudflarestorage.com")]
    [TestCase("https://unexpected.owner.test")]
    [TestCase("https://r2.cloudflarestorage.com")]
    [TestCase("https://technical-account.r2.cloudflarestorage.com/private")]
    [TestCase("https://user@technical-account.r2.cloudflarestorage.com")]
    [TestCase("https://technical-account.r2.cloudflarestorage.com/?signature=private")]
    [TestCase("https://technical-account.r2.cloudflarestorage.com/#fragment")]
    [TestCase("https://technical-account.r2.cloudflarestorage.com:8443")]
    public void R2Endpoint_WithUnsupportedAuthorityOrNonOriginComponents_IsRejected(string endpoint)
    {
        var options = Options();
        options.Endpoint = endpoint;
        Action validate = options.Validate;
        validate.Should().Throw<InvalidOperationException>();
    }

    [TestCase("not an absolute URI")]
    [TestCase("http://exercises-dev.owner.test")]
    [TestCase("https://exercises-dev.owner.test/path")]
    [TestCase("https://exercises-dev.owner.test/?signature=private")]
    [TestCase("https://exercises-dev.owner.test/#fragment")]
    [TestCase("https://user@exercises-dev.owner.test")]
    [TestCase("https://exercises-dev.owner.test:8443")]
    [TestCase("https://r2.dev.")]
    [TestCase("https://bucket.r2.dev.")]
    [TestCase("https://technical-account.r2.cloudflarestorage.com.")]
    [TestCase("https://r2.dev")]
    [TestCase("https://bucket.r2.dev")]
    [TestCase("https://r2.cloudflarestorage.com")]
    [TestCase("https://technical-account.r2.cloudflarestorage.com")]
    public void PublicOrigin_RejectsRawR2AndNonOriginComponents(string origin)
    {
        var options = Options();
        options.PublicBaseUrl = origin;
        Action validate = options.Validate;
        validate.Should().Throw<InvalidOperationException>();
    }

    [TestCase("tenant-one-assets")]
    [TestCase("tenant-two-images")]
    public void WorkerOriginPolicy_DoesNotDependOnBucketSpelling(string bucket)
    {
        var options = Options();
        options.Bucket = bucket;
        options.PublicBaseUrl = "https://exercise-media.operator.workers.dev";
        options.Validate();
    }

    [TestCase("AccessKeyId")]
    [TestCase("SecretAccessKey")]
    public void MissingDedicatedCredential_FailsClosed(string setting)
    {
        var options = Options();
        if (setting == "AccessKeyId") options.AccessKeyId = " ";
        else options.SecretAccessKey = " ";
        Action validate = options.Validate;
        validate.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public async Task DisabledStorage_DoesNotRequireCredentialsAndCannotUseProvider()
    {
        var options = new ExerciseMediaStorageOptions();
        options.Validate();
        using var storage = new ExerciseMediaStorage(options);
        storage.IsConfigured.Should().BeFalse();
        const string key = "exercises/00000000-0000-0000-0000-000000000001/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/animation.gif";
        Action url = () => storage.GetPublicUrl(key);
        url.Should().Throw<InvalidOperationException>();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await storage.PublishAsync(key, [1], "image/gif", default));
    }

    [TestCase("bucket")]
    [TestCase("credentials")]
    public void InfrastructureRegistration_RejectsSharedReportAuthority(string sharedAuthority)
    {
        var values = DedicatedConfiguration();
        if (sharedAuthority == "bucket") values["PhotoStorage:BucketName"] = values["ExerciseMediaStorage:Bucket"];
        else values["PhotoStorage:AccessKeyId"] = values["ExerciseMediaStorage:AccessKeyId"];
        Action register = () => new ServiceCollection().AddInfrastructure(TestConfigurationBuilder.BuildConfiguration(values), enableSensitiveLogging: false, isTesting: true);
        register.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void DisabledInfrastructure_IgnoresUnusedStorageSettingsAndStillResolvesExercisePersistence()
    {
        var values = DedicatedConfiguration();
        values["ExerciseMediaStorage:Enabled"] = "false";
        values["ExerciseMediaStorage:Bucket"] = "YOUR_EXERCISE_MEDIA_BUCKET";
        values["ExerciseMediaStorage:Endpoint"] = "not an enabled endpoint";
        values["ExerciseMediaStorage:AccessKeyId"] = values["PhotoStorage:AccessKeyId"];
        var services = new ServiceCollection();
        services.AddInfrastructure(TestConfigurationBuilder.BuildConfiguration(values), enableSensitiveLogging: false, isTesting: true);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExerciseMediaStorage>().IsConfigured.Should().BeFalse();
        scope.ServiceProvider.GetRequiredService<LgymApi.Application.WorkoutProgress.Persistence.IWorkoutExercisePersistence>().Should().NotBeNull();
    }

    [Test]
    public void InfrastructureRegistration_AcceptsOneDedicatedExerciseBucket()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(TestConfigurationBuilder.BuildConfiguration(DedicatedConfiguration()), enableSensitiveLogging: false, isTesting: true);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExerciseMediaStorage>().IsConfigured.Should().BeTrue();
        provider.GetRequiredService<ExerciseMediaStorageOptions>().Bucket.Should().Be("tenant-one-assets");
    }

    private static Dictionary<string, string?> DedicatedConfiguration()
    {
        var values = TestConfigurationBuilder.ToDictionary(CompositionRootTestHost.CreateConfiguration());
        var options = Options();
        values["ExerciseMediaStorage:Enabled"] = "true";
        values["ExerciseMediaStorage:Endpoint"] = options.Endpoint;
        values["ExerciseMediaStorage:AccessKeyId"] = options.AccessKeyId;
        values["ExerciseMediaStorage:SecretAccessKey"] = options.SecretAccessKey;
        values["ExerciseMediaStorage:Bucket"] = options.Bucket;
        values["ExerciseMediaStorage:PublicBaseUrl"] = options.PublicBaseUrl;
        values["PhotoStorage:BucketName"] = "report-private-dev";
        values["PhotoStorage:AccessKeyId"] = "synthetic-report-access";
        return values;
    }
}
