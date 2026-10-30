using System.Text.Json;

using LgymApi.Infrastructure.Reporting.PhotoTypesMigration;

namespace LgymApi.DataSeeder.ReportingPhotoTypesMigration;

public static class ReportingPhotoTypesMigrationConsole
{
    public static void Write(ReportingPhotoTypesMigrationReport report, TextWriter writer)
    {
        writer.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void WriteFailure(Exception exception, TextWriter writer)
    {
        var failure = new
        {
            Applied = false,
            Exceptions = new[]
            {
                new ReportingPhotoTypesMigrationIssue(
                    "Error",
                    "Global",
                    "MIGRATION_COMMAND_FAILED",
                    "migration",
                    $"The offline migration failed with {exception.GetType().Name}; no exception message, connection detail, or payload was emitted.")
            }
        };
        writer.WriteLine(JsonSerializer.Serialize(failure, new JsonSerializerOptions { WriteIndented = true }));
    }
}
