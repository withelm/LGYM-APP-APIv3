using LgymApi.Domain.ValueObjects;
using LgymApi.DataSeeder.ReportingPhotoTypesMigration;
using LgymApi.Infrastructure.Reporting.PhotoTypesMigration;

namespace LgymApi.DataSeeder.Tests;

[TestFixture]
public sealed class ProgramMainTests
{
    [Test]
    public async Task Main_Should_Return_NonZero_When_ConnectionString_Is_Missing()
    {
        var basePath = CreateTempRepo(withBaseSettings: true, includeConnection: false);
        var originalBasePath = Environment.GetEnvironmentVariable("LGYM_SEEDER_BASE_PATH");
        var originalTestMode = Environment.GetEnvironmentVariable("LGYM_SEEDER_TEST_MODE");
        var originalMigrationConnection = Environment.GetEnvironmentVariable("LGYM_MIGRATION_POSTGRES");
        var originalIn = Console.In;
        var originalOut = Console.Out;

        try
        {
            Environment.SetEnvironmentVariable("LGYM_SEEDER_BASE_PATH", basePath);
            Environment.SetEnvironmentVariable("LGYM_SEEDER_TEST_MODE", "true");
            Environment.SetEnvironmentVariable("LGYM_MIGRATION_POSTGRES", null);

            Console.SetIn(TextReader.Null);
            Console.SetOut(new StringWriter());

            var code = await Program.Main(Array.Empty<string>());

            Assert.That(code, Is.EqualTo(1));
        }
        finally
        {
            Environment.SetEnvironmentVariable("LGYM_SEEDER_BASE_PATH", originalBasePath);
            Environment.SetEnvironmentVariable("LGYM_SEEDER_TEST_MODE", originalTestMode);
            Environment.SetEnvironmentVariable("LGYM_MIGRATION_POSTGRES", originalMigrationConnection);
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
            Directory.Delete(basePath, recursive: true);
        }
    }

    [Test]
    public async Task Main_Should_Return_Zero_In_Test_Mode_When_Config_Is_Valid()
    {
        var basePath = CreateTempRepo(withBaseSettings: true);
        var originalBasePath = Environment.GetEnvironmentVariable("LGYM_SEEDER_BASE_PATH");
        var originalTestMode = Environment.GetEnvironmentVariable("LGYM_SEEDER_TEST_MODE");
        var originalMigrationConnection = Environment.GetEnvironmentVariable("LGYM_MIGRATION_POSTGRES");
        var originalIn = Console.In;
        var originalOut = Console.Out;

        try
        {
            Environment.SetEnvironmentVariable("LGYM_SEEDER_BASE_PATH", basePath);
            Environment.SetEnvironmentVariable("LGYM_SEEDER_TEST_MODE", "true");
            Environment.SetEnvironmentVariable("LGYM_MIGRATION_POSTGRES", "Host=maintenance;Database=seeder;Username=maintenance;Password=test-only");

            Console.SetIn(TextReader.Null);
            Console.SetOut(new StringWriter());

            var code = await Program.Main(Array.Empty<string>());

            Assert.That(code, Is.EqualTo(0));
        }
        finally
        {
            Environment.SetEnvironmentVariable("LGYM_SEEDER_BASE_PATH", originalBasePath);
            Environment.SetEnvironmentVariable("LGYM_SEEDER_TEST_MODE", originalTestMode);
            Environment.SetEnvironmentVariable("LGYM_MIGRATION_POSTGRES", originalMigrationConnection);
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
            Directory.Delete(basePath, recursive: true);
        }
    }

    [TestCase(new[] { "--migrate-only" }, SeederMode.MigrateOnly)]
    [TestCase(new[] { "--prepare-hangfire" }, SeederMode.PrepareHangfire)]
    [TestCase(new[] { "--seed" }, SeederMode.Seed)]
    [TestCase(new string[0], SeederMode.Seed)]
    public void TryParseMode_RecognizesSupportedOfflineModes(string[] args, SeederMode expectedMode)
    {
        Assert.That(Program.TryParseMode(args, out var mode), Is.True);
        Assert.That(mode, Is.EqualTo(expectedMode));
    }

    [Test]
    public void TryParseMode_RejectsUnknownOrCombinedModes()
    {
        Assert.That(Program.TryParseMode(["--migrate-only", "--prepare-hangfire"], out _), Is.False);
        Assert.That(Program.TryParseMode(["--unknown"], out _), Is.False);
    }

    [Test]
    public async Task Main_Help_ReturnsWithoutConfigurationOrDatabaseAccess()
    {
        var originalOut = Console.Out;
        var output = new StringWriter();
        try
        {
            Console.SetOut(output);

            var code = await Program.Main(["--help"]);

            Assert.That(code, Is.EqualTo(0));
            Assert.That(output.ToString(), Does.Contain("reporting-photo-types --dry-run"));
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Test]
    public void ReportingMigrationCommand_ApplyRequiresFingerprintAndExactFenceAcknowledgement()
    {
        Assert.That(ReportingPhotoTypesMigrationCommand.TryParse(
            ["reporting-photo-types", "--apply"],
            out _,
            out var missingGuardError), Is.False);
        Assert.That(missingGuardError, Does.Contain("acknowledge-external-fence"));

        Assert.That(ReportingPhotoTypesMigrationCommand.TryParse(
            [
                "reporting-photo-types",
                "--apply",
                "--expected-fingerprint=ABC",
                $"--acknowledge-external-fence={ReportingPhotoTypesMigrationCommand.ExternalFenceAcknowledgement}"
            ],
            out var options,
            out _), Is.True);
        Assert.That(options!.Mode, Is.EqualTo(ReportingPhotoTypesMigrationMode.Apply));
        Assert.That(options.ExpectedFingerprint, Is.EqualTo("ABC"));
        Assert.That(options.ExternalFenceAcknowledged, Is.True);
    }

    private static string CreateTempRepo(bool withBaseSettings, bool includeConnection = true)
    {
        var root = Path.Combine(Path.GetTempPath(), "lgym-seeder-program", Id<ProgramMainTests>.New().ToString());
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "LgymApi.slnx"), string.Empty);

        var apiRoot = Path.Combine(root, "LgymApi.Api");
        Directory.CreateDirectory(apiRoot);

        if (withBaseSettings)
        {
            var baseSettings = includeConnection
                ? "{" + "\"ConnectionStrings\": { \"Postgres\": \"Host=localhost\" }" + "}"
                : "{" + "\"ConnectionStrings\": {}" + "}";
            File.WriteAllText(Path.Combine(apiRoot, "appsettings.json"), baseSettings);
        }

        return root;
    }
}
