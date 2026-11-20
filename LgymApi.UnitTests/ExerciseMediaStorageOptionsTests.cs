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
        Action validate = () => options.Validate();
        Assert.That(() => { validate(); }, Throws.InstanceOf<InvalidOperationException>());
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
        Action validate = () => options.Validate();
        Assert.That(() => { validate(); }, Throws.InstanceOf<InvalidOperationException>());
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
        Action validate = () => options.Validate();
        Assert.That(() => { validate(); }, Throws.InstanceOf<InvalidOperationException>());
    }

    [TestCase("tenant-one-assets")]
    [TestCase("tenant-two-images")]
    public void PublicBucketOrigin_DoesNotDependOnBucketSpelling(string bucket)
    {
        var options = Options();
        options.Bucket = bucket;
        options.PublicBaseUrl = "https://exercise-media.owner.test";
        options.Validate();
    }

    [TestCase("Development", false, false)]
    [TestCase("Development", true, true)]
    [TestCase("Production", true, false)]
    [TestCase("Testing", true, false)]
    [TestCase("Staging", true, false)]
    public void R2Dev_RequiresExplicitOptInAndDevelopmentHost(string environmentName, bool optIn, bool accepted)
    {
        var options = Options();
        options.AllowDevelopmentR2Dev = optIn;
        options.PublicBaseUrl = "https://pub-example.r2.dev";
        Action validate = () => options.Validate(environmentName);
        if (accepted) Assert.That(() => { validate(); }, Throws.Nothing);
        else Assert.That(() => { validate(); }, Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public void DevelopmentRegistration_AndStorageShareTheValidatedPublicOrigin()
    {
        var values = DedicatedConfiguration();
        values["ExerciseMediaStorage:PublicBaseUrl"] = "https://pub-example.r2.dev";
        values["ExerciseMediaStorage:AllowDevelopmentR2Dev"] = "true";
        var services = new ServiceCollection();
        services.AddInfrastructure(TestConfigurationBuilder.BuildConfiguration(values), enableSensitiveLogging: false, isTesting: true, environmentName: "Development");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        const string key = "exercises/00000000-0000-0000-0000-000000000001/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/image.png";
        Assert.That(((ExerciseMediaStorage)scope.ServiceProvider.GetRequiredService<IExerciseMediaStorage>()).GetPublicUrl(key), Is.EqualTo("https://pub-example.r2.dev/" + key));
        Action production = () => new ServiceCollection().AddInfrastructure(TestConfigurationBuilder.BuildConfiguration(values), enableSensitiveLogging: false);
        Assert.That(() => { production(); }, Throws.InstanceOf<InvalidOperationException>());
    }

    [TestCase("AccessKeyId")]
    [TestCase("SecretAccessKey")]
    public void MissingDedicatedCredential_FailsClosed(string setting)
    {
        var options = Options();
        if (setting == "AccessKeyId") options.AccessKeyId = " ";
        else options.SecretAccessKey = " ";
        Action validate = () => options.Validate();
        Assert.That(() => { validate(); }, Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public async Task DisabledStorage_DoesNotRequireCredentialsAndCannotUseProvider()
    {
        var options = new ExerciseMediaStorageOptions();
        options.Validate();
        using var storage = new ExerciseMediaStorage(options);
        Assert.That(storage.IsConfigured, Is.False);
        const string key = "exercises/00000000-0000-0000-0000-000000000001/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/animation.gif";
        Action url = () => storage.GetPublicUrl(key);
        Assert.That(() => { url(); }, Throws.InstanceOf<InvalidOperationException>());
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
        Assert.That(() => { register(); }, Throws.InstanceOf<InvalidOperationException>());
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
        Assert.That(scope.ServiceProvider.GetRequiredService<IExerciseMediaStorage>().IsConfigured, Is.False);
        Assert.That(scope.ServiceProvider.GetRequiredService<LgymApi.Application.WorkoutProgress.Persistence.IWorkoutExercisePersistence>(), Is.Not.Null);
    }

    [Test]
    public void InfrastructureRegistration_AcceptsOneDedicatedExerciseBucket()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(TestConfigurationBuilder.BuildConfiguration(DedicatedConfiguration()), enableSensitiveLogging: false, isTesting: true);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<IExerciseMediaStorage>().IsConfigured, Is.True);
        Assert.That(provider.GetRequiredService<ExerciseMediaStorageOptions>().Bucket, Is.EqualTo("tenant-one-assets"));
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
