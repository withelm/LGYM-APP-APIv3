using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace LgymApi.Api.Configuration;

public static class ExternalConfigBootstrap
{
    private const string ExternalConfigPathEnvironmentVariable = "LGYM_APP_CONFIG_PATH";
    private const string ContainerEnvironmentVariable = "DOTNET_RUNNING_IN_CONTAINER";

    public static void Configure(
        ConfigurationManager configuration,
        string contentRootPath,
        string environmentName,
        string[] args)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);
        ArgumentNullException.ThrowIfNull(args);

        var isRunningInContainer = IsRunningInContainer();
        var externalConfigPathValue = Environment.GetEnvironmentVariable(ExternalConfigPathEnvironmentVariable);

        configuration.Sources.Clear();
        configuration.SetBasePath(contentRootPath);
        configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
        configuration.AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false);

        if (externalConfigPathValue is null)
        {
            if (isRunningInContainer)
            {
                ThrowBootstrapFailure("missing-path");
            }
        }
        else if (string.IsNullOrWhiteSpace(externalConfigPathValue))
        {
            ThrowBootstrapFailure("empty-path");
        }
        else
        {
            var normalizedExternalConfigPath = NormalizePath(externalConfigPathValue, contentRootPath);

            EnsureJsonFileCanBeLoaded(normalizedExternalConfigPath);
            configuration.AddJsonFile(normalizedExternalConfigPath, optional: false, reloadOnChange: false);
        }

        configuration.AddEnvironmentVariables();
        configuration.AddCommandLine(args);
    }

    private static bool IsRunningInContainer()
        => string.Equals(
            Environment.GetEnvironmentVariable(ContainerEnvironmentVariable),
            bool.TrueString,
            StringComparison.OrdinalIgnoreCase);

    private static string NormalizePath(string path, string contentRootPath)
        => Path.GetFullPath(path.Trim(), contentRootPath);

    private static void EnsureJsonFileCanBeLoaded(string normalizedPath)
    {
        try
        {
            using var stream = File.OpenRead(normalizedPath);
            using var _ = JsonDocument.Parse(stream);
        }
        catch (JsonException exception)
        {
            ThrowBootstrapFailure("invalid-json", normalizedPath, exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ThrowBootstrapFailure("unreadable-file", normalizedPath, exception);
        }
    }

    private static void ThrowBootstrapFailure(string category, string? normalizedPath = null, Exception? innerException = null)
    {
        var logMessage = normalizedPath is null
            ? $"Startup configuration bootstrap failed. category={category}"
            : $"Startup configuration bootstrap failed. category={category}; path={normalizedPath}";

        Console.Error.WriteLine(logMessage);

        var message = normalizedPath is null
            ? $"Startup configuration bootstrap failed with category '{category}'. Set {ExternalConfigPathEnvironmentVariable} when {ContainerEnvironmentVariable}=true."
            : $"Startup configuration bootstrap failed with category '{category}' for path '{normalizedPath}'.";

        throw new InvalidOperationException(message, innerException);
    }
}
