using LgymApi.Infrastructure.Reporting.PhotoTypesMigration;

namespace LgymApi.DataSeeder.ReportingPhotoTypesMigration;

public static class ReportingPhotoTypesMigrationCommand
{
    public const string CommandName = "reporting-photo-types";
    public const string ExternalFenceAcknowledgement = "LGYM103-REPORTING-WRITERS-STOPPED";

    public static bool TryParse(string[] args, out ReportingPhotoTypesMigrationOptions? options, out string? error)
    {
        options = null;
        error = null;

        if (args.Length < 2 || !string.Equals(args[0], CommandName, StringComparison.Ordinal))
        {
            error = "Expected reporting-photo-types followed by --dry-run or --apply.";
            return false;
        }

        var mode = args[1] switch
        {
            "--dry-run" => ReportingPhotoTypesMigrationMode.DryRun,
            "--apply" => ReportingPhotoTypesMigrationMode.Apply,
            _ => (ReportingPhotoTypesMigrationMode?)null
        };
        if (!mode.HasValue)
        {
            error = "Migration mode must be exactly --dry-run or --apply.";
            return false;
        }

        var batchSize = 100;
        string? expectedFingerprint = null;
        string? acknowledgement = null;
        var allowRetainedDeletedOwners = false;

        foreach (var argument in args.Skip(2))
        {
            if (argument.StartsWith("--batch-size=", StringComparison.Ordinal))
            {
                if (!int.TryParse(argument["--batch-size=".Length..], out batchSize) || batchSize is < 1 or > 1000)
                {
                    error = "--batch-size must be between 1 and 1000.";
                    return false;
                }
            }
            else if (argument.StartsWith("--expected-fingerprint=", StringComparison.Ordinal))
            {
                expectedFingerprint = argument["--expected-fingerprint=".Length..];
            }
            else if (argument.StartsWith("--acknowledge-external-fence=", StringComparison.Ordinal))
            {
                acknowledgement = argument["--acknowledge-external-fence=".Length..];
            }
            else if (string.Equals(argument, "--allow-retained-deleted-owners", StringComparison.Ordinal))
            {
                allowRetainedDeletedOwners = true;
            }
            else
            {
                error = $"Unknown reporting migration option '{argument}'.";
                return false;
            }
        }

        if (mode == ReportingPhotoTypesMigrationMode.Apply
            && !string.Equals(acknowledgement, ExternalFenceAcknowledgement, StringComparison.Ordinal))
        {
            error = $"--apply requires --acknowledge-external-fence={ExternalFenceAcknowledgement}.";
            return false;
        }

        if (mode == ReportingPhotoTypesMigrationMode.Apply && string.IsNullOrWhiteSpace(expectedFingerprint))
        {
            error = "--apply requires --expected-fingerprint from the immediately preceding dry run.";
            return false;
        }

        options = new ReportingPhotoTypesMigrationOptions(
            mode.Value,
            batchSize,
            expectedFingerprint,
            allowRetainedDeletedOwners,
            mode == ReportingPhotoTypesMigrationMode.Apply);
        return true;
    }

    public static void PrintHelp(TextWriter writer)
    {
        writer.WriteLine("Reporting photo-type snapshot migration (offline only)");
        writer.WriteLine("  reporting-photo-types --dry-run [--batch-size=100] [--allow-retained-deleted-owners]");
        writer.WriteLine($"  reporting-photo-types --apply --expected-fingerprint=<SHA256> --acknowledge-external-fence={ExternalFenceAcknowledgement} [--batch-size=100] [--allow-retained-deleted-owners]");
        writer.WriteLine("Dry-run performs no writes. Apply requires LGYM_MIGRATION_POSTGRES, a stopped old-writer fence, and an unchanged dry-run fingerprint.");
    }
}
