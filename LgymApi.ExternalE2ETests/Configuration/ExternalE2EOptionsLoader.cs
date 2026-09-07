using System.Data.Common;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace LgymApi.ExternalE2ETests.Configuration;

internal static class ExternalE2EOptionsLoader
{
    private const string EnvironmentVariablePrefix = "LGYM_EXTERNAL_E2E__";
    private const int MinimumTimeoutSeconds = 1;
    private const int MaximumTimeoutSeconds = 900;
    private static readonly Regex DedicatedDatabaseNamePattern = new("^lgym_external_e2e(?:_[a-z0-9]+)*$", RegexOptions.CultureInvariant);
    private static readonly Regex DedicatedUserNamePattern = new("^lgym_external_e2e(?:_[a-z0-9]+)*$", RegexOptions.CultureInvariant);
    private static readonly Regex BaselineMarkerPattern = new("^lgym_external_e2e_v[1-9][0-9]*$", RegexOptions.CultureInvariant);

    public static ExternalE2EOptions Load(string configurationPath)
    {
        var configuration = BuildConfiguration(configurationPath);
        var raw = configuration.Get<ExternalE2ERawOptions>() ?? new ExternalE2ERawOptions();
        var apiBaseUrl = ParseBaseUrl(raw.ApiBaseUrl, nameof(raw.ApiBaseUrl));
        var webBaseUrl = ParseBaseUrl(raw.WebBaseUrl, nameof(raw.WebBaseUrl));
        var expectedDatabaseName = ParseDedicatedDatabaseName(raw.ExpectedDatabaseName);
        var connection = ParseConnection(raw.ConnectionString, expectedDatabaseName);

        return new ExternalE2EOptions
        {
            ApiBaseUrl = apiBaseUrl,
            WebBaseUrl = webBaseUrl,
            ConnectionString = Require(raw.ConnectionString, nameof(raw.ConnectionString)),
            ConnectionIdentity = connection,
            BaselineDumpPath = ParseCustomDumpPath(raw.BaselineDumpPath),
            ExpectedDatabaseName = expectedDatabaseName,
            ExpectedDatabaseMarker = ParseBaselineMarker(raw.ExpectedDatabaseMarker),
            RestoreTimeout = ParseTimeout(raw.RestoreTimeoutSeconds, nameof(raw.RestoreTimeoutSeconds)),
            ApiRecoveryTimeout = ParseTimeout(raw.ApiRecoveryTimeoutSeconds, nameof(raw.ApiRecoveryTimeoutSeconds)),
            BrowserTimeout = ParseTimeout(raw.BrowserTimeoutSeconds, nameof(raw.BrowserTimeoutSeconds)),
            DiagnosticTimeout = ParseTimeout(raw.DiagnosticTimeoutSeconds, nameof(raw.DiagnosticTimeoutSeconds)),
            ArtifactRoot = ParseArtifactRoot(raw.ArtifactRoot)
        };
    }

    private static IConfigurationRoot BuildConfiguration(string configurationPath)
    {
        if (string.IsNullOrWhiteSpace(configurationPath))
        {
            throw InvalidConfiguration("configuration file");
        }

        try
        {
            var fullConfigurationPath = Path.GetFullPath(configurationPath);
            if (!File.Exists(fullConfigurationPath))
            {
                throw InvalidConfiguration("configuration file");
            }

            return new ConfigurationBuilder()
                .SetBasePath(Path.GetDirectoryName(fullConfigurationPath)!)
                .AddJsonFile(Path.GetFileName(fullConfigurationPath), optional: false, reloadOnChange: false)
                .AddEnvironmentVariables(EnvironmentVariablePrefix)
                .Build();
        }
        catch (InvalidOperationException exception) when (exception.Message.StartsWith("External E2E runtime configuration", StringComparison.Ordinal))
        {
            throw;
        }
        catch (Exception)
        {
            throw InvalidConfiguration("configuration file");
        }
    }

    private static Uri ParseBaseUrl(string? value, string fieldName)
    {
        var url = Require(value, fieldName);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw InvalidConfiguration(fieldName);
        }

        return uri;
    }

    private static ExternalE2EConnectionIdentity ParseConnection(string? value, string expectedDatabaseName)
    {
        var connectionString = Require(value, nameof(ExternalE2ERawOptions.ConnectionString));
        var builder = new DbConnectionStringBuilder();

        try
        {
            builder.ConnectionString = connectionString;
        }
        catch (ArgumentException)
        {
            throw InvalidConfiguration(nameof(ExternalE2ERawOptions.ConnectionString));
        }

        var host = GetConnectionValue(builder, "Host");
        var databaseName = GetConnectionValue(builder, "Database");
        var userName = GetConnectionValue(builder, "Username");

        if (!IsPermittedDatabaseHost(host) ||
            !DedicatedDatabaseNamePattern.IsMatch(databaseName) ||
            !string.Equals(databaseName, expectedDatabaseName, StringComparison.Ordinal) ||
            !DedicatedUserNamePattern.IsMatch(userName))
        {
            throw InvalidConfiguration(nameof(ExternalE2ERawOptions.ConnectionString));
        }

        return new ExternalE2EConnectionIdentity(host, databaseName, userName);
    }

    private static string GetConnectionValue(DbConnectionStringBuilder builder, string key)
    {
        if (!builder.TryGetValue(key, out var value) || value is not string setting || string.IsNullOrWhiteSpace(setting))
        {
            throw InvalidConfiguration(nameof(ExternalE2ERawOptions.ConnectionString));
        }

        return setting.Trim();
    }

    private static bool IsPermittedDatabaseHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(host, out var address))
        {
            return false;
        }

        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 &&
               (bytes[0] == 10 ||
                (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168));
    }

    private static string ParseDedicatedDatabaseName(string? value)
    {
        var databaseName = Require(value, nameof(ExternalE2ERawOptions.ExpectedDatabaseName));
        if (!DedicatedDatabaseNamePattern.IsMatch(databaseName))
        {
            throw InvalidConfiguration(nameof(ExternalE2ERawOptions.ExpectedDatabaseName));
        }

        return databaseName;
    }

    private static string ParseCustomDumpPath(string? value)
    {
        var dumpPath = Require(value, nameof(ExternalE2ERawOptions.BaselineDumpPath));
        if (!Path.IsPathFullyQualified(dumpPath) || !File.Exists(dumpPath))
        {
            throw InvalidConfiguration(nameof(ExternalE2ERawOptions.BaselineDumpPath));
        }

        try
        {
            using var stream = File.OpenRead(dumpPath);
            Span<byte> header = stackalloc byte[5];
            if (stream.Read(header) != header.Length || !header.SequenceEqual("PGDMP"u8))
            {
                throw InvalidConfiguration(nameof(ExternalE2ERawOptions.BaselineDumpPath));
            }
        }
        catch (InvalidOperationException exception) when (exception.Message.StartsWith("External E2E runtime configuration", StringComparison.Ordinal))
        {
            throw;
        }
        catch (Exception)
        {
            throw InvalidConfiguration(nameof(ExternalE2ERawOptions.BaselineDumpPath));
        }

        return Path.GetFullPath(dumpPath);
    }

    private static string ParseBaselineMarker(string? value)
    {
        var marker = Require(value, nameof(ExternalE2ERawOptions.ExpectedDatabaseMarker));
        if (!BaselineMarkerPattern.IsMatch(marker))
        {
            throw InvalidConfiguration(nameof(ExternalE2ERawOptions.ExpectedDatabaseMarker));
        }

        return marker;
    }

    private static TimeSpan ParseTimeout(string? value, string fieldName)
    {
        if (!int.TryParse(value, out var timeoutSeconds) ||
            timeoutSeconds < MinimumTimeoutSeconds ||
            timeoutSeconds > MaximumTimeoutSeconds)
        {
            throw InvalidConfiguration(fieldName);
        }

        return TimeSpan.FromSeconds(timeoutSeconds);
    }

    private static string ParseArtifactRoot(string? value)
    {
        var artifactRoot = Require(value, nameof(ExternalE2ERawOptions.ArtifactRoot));
        if (!Path.IsPathFullyQualified(artifactRoot))
        {
            throw InvalidConfiguration(nameof(ExternalE2ERawOptions.ArtifactRoot));
        }

        try
        {
            var fullArtifactRoot = Path.GetFullPath(artifactRoot);
            if (string.Equals(fullArtifactRoot, Path.GetPathRoot(fullArtifactRoot), StringComparison.OrdinalIgnoreCase))
            {
                throw InvalidConfiguration(nameof(ExternalE2ERawOptions.ArtifactRoot));
            }

            return fullArtifactRoot;
        }
        catch (InvalidOperationException exception) when (exception.Message.StartsWith("External E2E runtime configuration", StringComparison.Ordinal))
        {
            throw;
        }
        catch (Exception)
        {
            throw InvalidConfiguration(nameof(ExternalE2ERawOptions.ArtifactRoot));
        }
    }

    private static string Require(string? value, string fieldName)
        => string.IsNullOrWhiteSpace(value)
            ? throw InvalidConfiguration(fieldName)
            : value.Trim();

    private static InvalidOperationException InvalidConfiguration(string fieldName)
        => new($"External E2E runtime configuration is invalid: {fieldName}.");

    private sealed class ExternalE2ERawOptions
    {
        public string? ApiBaseUrl { get; init; }

        public string? WebBaseUrl { get; init; }

        public string? ConnectionString { get; init; }

        public string? BaselineDumpPath { get; init; }

        public string? ExpectedDatabaseName { get; init; }

        public string? ExpectedDatabaseMarker { get; init; }

        public string? RestoreTimeoutSeconds { get; init; }

        public string? ApiRecoveryTimeoutSeconds { get; init; }

        public string? BrowserTimeoutSeconds { get; init; }

        public string? DiagnosticTimeoutSeconds { get; init; }

        public string? ArtifactRoot { get; init; }
    }
}
