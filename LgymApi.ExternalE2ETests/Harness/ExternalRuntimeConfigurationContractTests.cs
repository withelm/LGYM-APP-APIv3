using System.Text;
using System.Text.Json;
using LgymApi.ExternalE2ETests.Configuration;
using NUnit.Framework;

namespace LgymApi.ExternalE2ETests.Harness;

[TestFixture]
[Category("Configuration")]
public sealed class ExternalRuntimeConfigurationContractTests
{
    [Test]
    public void Test_external_runtime_configuration_contract_when_local_configuration_is_missing()
    {
        using var fixture = ExternalE2EConfigurationFixture.Create();
        File.Delete(fixture.ConfigurationPath);

        var exception = Assert.Throws<InvalidOperationException>(
            () => ExternalE2EOptionsLoader.Load(fixture.ConfigurationPath));

        Assert.That(exception!.Message, Is.EqualTo("External E2E runtime configuration is invalid: configuration file."));
    }

    [Test]
    public void Test_external_runtime_configuration_contract_when_json_is_valid_returns_typed_options()
    {
        using var fixture = ExternalE2EConfigurationFixture.Create();

        var options = ExternalE2EOptionsLoader.Load(fixture.ConfigurationPath);

        Assert.Multiple(() =>
        {
            Assert.That(options.ApiBaseUrl, Is.EqualTo(new Uri("http://127.0.0.1:5080/")));
            Assert.That(options.WebBaseUrl, Is.EqualTo(new Uri("http://127.0.0.1:8081/")));
            Assert.That(options.ConnectionIdentity, Is.EqualTo(new ExternalE2EConnectionIdentity(
                "127.0.0.1",
                "lgym_external_e2e_local",
                "lgym_external_e2e")));
            Assert.That(options.ExpectedDatabaseName, Is.EqualTo("lgym_external_e2e_local"));
            Assert.That(options.ExpectedDatabaseMarker, Is.EqualTo("lgym_external_e2e_v1"));
            Assert.That(options.RestoreTimeout, Is.EqualTo(TimeSpan.FromSeconds(300)));
            Assert.That(options.ApiRecoveryTimeout, Is.EqualTo(TimeSpan.FromSeconds(30)));
            Assert.That(options.BrowserTimeout, Is.EqualTo(TimeSpan.FromSeconds(30)));
            Assert.That(options.DiagnosticTimeout, Is.EqualTo(TimeSpan.FromSeconds(15)));
            Assert.That(options.ArtifactRoot, Is.EqualTo(Path.GetFullPath(fixture.ArtifactRoot)));
        });
    }

    [Test]
    public void Test_external_runtime_configuration_contract_when_environment_override_is_set_wins_over_json()
    {
        using var fixture = ExternalE2EConfigurationFixture.Create();
        const string variableName = "LGYM_EXTERNAL_E2E__ApiBaseUrl";
        var originalValue = Environment.GetEnvironmentVariable(variableName);

        Environment.SetEnvironmentVariable(variableName, "http://127.0.0.1:5099");

        try
        {
            var options = ExternalE2EOptionsLoader.Load(fixture.ConfigurationPath);

            Assert.That(options.ApiBaseUrl, Is.EqualTo(new Uri("http://127.0.0.1:5099/")));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, originalValue);
        }
    }

    [TestCase("ApiBaseUrl")]
    [TestCase("WebBaseUrl")]
    [TestCase("ConnectionString")]
    [TestCase("BaselineDumpPath")]
    [TestCase("ExpectedDatabaseName")]
    [TestCase("ExpectedDatabaseMarker")]
    [TestCase("RestoreTimeoutSeconds")]
    [TestCase("ApiRecoveryTimeoutSeconds")]
    [TestCase("BrowserTimeoutSeconds")]
    [TestCase("DiagnosticTimeoutSeconds")]
    [TestCase("ArtifactRoot")]
    public void Test_external_runtime_configuration_contract_when_required_setting_is_missing_fails_before_external_work(string settingName)
    {
        using var fixture = ExternalE2EConfigurationFixture.Create();
        fixture.Set(settingName, null);

        AssertInvalidConfiguration(fixture.ConfigurationPath, settingName);
    }

    [Test]
    public void Test_external_runtime_configuration_contract_when_url_contains_userinfo_rejects_it()
    {
        using var fixture = ExternalE2EConfigurationFixture.Create();
        fixture.Set("ApiBaseUrl", "http://user@127.0.0.1:5080");

        AssertInvalidConfiguration(fixture.ConfigurationPath, "ApiBaseUrl");
    }

    [Test]
    public void Test_external_runtime_configuration_contract_when_database_name_is_not_dedicated_rejects_it()
    {
        using var fixture = ExternalE2EConfigurationFixture.Create();
        fixture.Set("ExpectedDatabaseName", "lgym_local");

        AssertInvalidConfiguration(fixture.ConfigurationPath, "ExpectedDatabaseName");
    }

    [Test]
    public void Test_external_runtime_configuration_contract_when_database_host_is_public_rejects_it()
    {
        using var fixture = ExternalE2EConfigurationFixture.Create();
        fixture.Set("ConnectionString", "Host=198.51.100.10;Database=lgym_external_e2e_local;Username=lgym_external_e2e");

        AssertInvalidConfiguration(fixture.ConfigurationPath, "ConnectionString");
    }

    [Test]
    public void Test_external_runtime_configuration_contract_when_marker_format_is_unexpected_rejects_it()
    {
        using var fixture = ExternalE2EConfigurationFixture.Create();
        fixture.Set("ExpectedDatabaseMarker", "unexpected_marker");

        AssertInvalidConfiguration(fixture.ConfigurationPath, "ExpectedDatabaseMarker");
    }

    [Test]
    public void Test_external_runtime_configuration_contract_when_timeout_is_unbounded_rejects_it()
    {
        using var fixture = ExternalE2EConfigurationFixture.Create();
        fixture.Set("BrowserTimeoutSeconds", "0");

        AssertInvalidConfiguration(fixture.ConfigurationPath, "BrowserTimeoutSeconds");
    }

    [Test]
    public void Test_external_runtime_configuration_contract_when_artifact_root_is_not_private_rejects_it()
    {
        using var fixture = ExternalE2EConfigurationFixture.Create();
        fixture.Set("ArtifactRoot", "artifacts");

        AssertInvalidConfiguration(fixture.ConfigurationPath, "ArtifactRoot");
    }

    [Test]
    public void Test_external_runtime_configuration_contract_when_dump_is_not_pg_restore_compatible_rejects_it()
    {
        using var fixture = ExternalE2EConfigurationFixture.Create();
        File.WriteAllText(fixture.DumpPath, "not a PostgreSQL custom dump", Encoding.ASCII);

        AssertInvalidConfiguration(fixture.ConfigurationPath, "BaselineDumpPath");
    }

    private static void AssertInvalidConfiguration(string configurationPath, string expectedField)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => ExternalE2EOptionsLoader.Load(configurationPath));

        Assert.That(
            exception!.Message,
            Is.EqualTo($"External E2E runtime configuration is invalid: {expectedField}."));
    }
}

internal sealed class ExternalE2EConfigurationFixture : IDisposable
{
    private readonly Dictionary<string, string?> settings;

    private ExternalE2EConfigurationFixture(string root)
    {
        Root = root;
        ConfigurationPath = Path.Combine(root, "appsettings.ExternalE2E.json");
        DumpPath = Path.Combine(root, "baseline.dump");
        ArtifactRoot = Path.Combine(root, "artifacts");
        settings = new Dictionary<string, string?>
        {
            ["ApiBaseUrl"] = "http://127.0.0.1:5080",
            ["WebBaseUrl"] = "http://127.0.0.1:8081",
            ["ConnectionString"] = "Host=127.0.0.1;Database=lgym_external_e2e_local;Username=lgym_external_e2e",
            ["BaselineDumpPath"] = DumpPath,
            ["ExpectedDatabaseName"] = "lgym_external_e2e_local",
            ["ExpectedDatabaseMarker"] = "lgym_external_e2e_v1",
            ["RestoreTimeoutSeconds"] = "300",
            ["ApiRecoveryTimeoutSeconds"] = "30",
            ["BrowserTimeoutSeconds"] = "30",
            ["DiagnosticTimeoutSeconds"] = "15",
            ["ArtifactRoot"] = ArtifactRoot
        };
    }

    public string Root { get; }

    public string ConfigurationPath { get; }

    public string DumpPath { get; }

    public string ArtifactRoot { get; }

    public static ExternalE2EConfigurationFixture Create()
    {
        var root = Path.Combine(Path.GetTempPath(), $"lgym-external-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var fixture = new ExternalE2EConfigurationFixture(root);
        Directory.CreateDirectory(fixture.ArtifactRoot);
        File.WriteAllBytes(fixture.DumpPath, "PGDMP\u0001\u000e\0\0"u8.ToArray());
        fixture.WriteConfiguration();
        return fixture;
    }

    public void Set(string settingName, string? value)
    {
        settings[settingName] = value;
        WriteConfiguration();
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);

    private void WriteConfiguration()
        => File.WriteAllText(
            ConfigurationPath,
            JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
}
