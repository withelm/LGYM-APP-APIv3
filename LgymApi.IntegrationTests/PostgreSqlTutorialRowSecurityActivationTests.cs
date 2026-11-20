using System.Diagnostics;
using System.Reflection;
using Npgsql;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class PostgreSqlTutorialRowSecurityActivationTests
{
    [Test]
    public void CreateProcessStartInfo_UsesActivationScriptDirectoryForRelativeIncludes()
    {
        var method = typeof(PostgreSqlTutorialRowSecurityActivation).GetMethod(
            "CreateProcessStartInfo",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);

        var startInfo = (ProcessStartInfo)method!.Invoke(null,
        [
            new NpgsqlConnectionStringBuilder("Host=localhost;Database=postgres;Username=postgres"),
            "postgres",
            "maintenance",
            "runtime",
            "Staging"
        ])!;

        Assert.That(startInfo.WorkingDirectory, Is.EqualTo(Path.Combine(FindRepositoryRoot(), "deploy", "postgres")));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LgymApi.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
