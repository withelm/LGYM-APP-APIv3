using Microsoft.Extensions.Configuration;

namespace LgymApi.DataSeeder;

public static class DataSeederProgram
{
    public static IConfiguration BuildConfiguration(string basePath)
    {
        var repoRoot = ResolveRepositoryRoot(basePath);
        var apiRoot = Path.Combine(repoRoot, "LgymApi.Api");
        var appSettingsPath = Path.Combine(apiRoot, "appsettings.json");

        var builder = new ConfigurationBuilder()
            .SetBasePath(repoRoot)
            .AddJsonFile(appSettingsPath, optional: false, reloadOnChange: false);

        var optionalSettings = Directory
            .EnumerateFiles(apiRoot, "appsettings.*.json", SearchOption.TopDirectoryOnly)
            .Where(path => !path.EndsWith("appsettings.json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        foreach (var optionalPath in optionalSettings)
        {
            builder.AddJsonFile(optionalPath, optional: true, reloadOnChange: false);
        }

        return builder
            .AddEnvironmentVariables()
            .Build();
    }

    public static string ResolveRepositoryRoot(string startPath)
    {
        var current = new DirectoryInfo(startPath);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "LgymApi.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }

    public static string MaskConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "<empty>";
        }

        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].StartsWith("Password=", StringComparison.OrdinalIgnoreCase))
            {
                parts[i] = "Password=***";
            }
        }

        return string.Join(';', parts);
    }
}
