using LgymApi.Domain.Security;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Reporting.PhotoTypesMigration;

/// <summary>Runs before the host and hosted jobs start. Deployment must stop old writers first.</summary>
public static class ReportingPhotoTypesStartupMigration
{
    public static async Task ApplyAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        // A real self-update serializes new bootstraps without changing role values or timestamps.
        var locked = await context.Roles.IgnoreQueryFilters()
            .Where(role => role.Name == AuthConstants.Roles.Trainer && !role.IsDeleted)
            .StageUpdateAsync(context, role => role.UpdatedAt, role => role.UpdatedAt, cancellationToken);
        if (locked != 1)
            throw new InvalidOperationException("Reporting startup migration requires the seeded Trainer role.");

        if (!await HasMissingSnapshotsAsync(context, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        var runner = new ReportingPhotoTypesMigrationRunner(context);
        var options = new ReportingPhotoTypesMigrationOptions(ReportingPhotoTypesMigrationMode.DryRun, 100, null, false, false, IgnoreExpiredPendingUploads: true);
        var preview = await runner.RunAsync(options, cancellationToken);
        ThrowIfBlocked(preview);
        var applied = await runner.RunAsync(options with
        {
            Mode = ReportingPhotoTypesMigrationMode.Apply,
            ExpectedFingerprint = preview.Fingerprint,
            ExternalFenceAcknowledged = true
        }, cancellationToken);
        ThrowIfBlocked(applied);
        if (!applied.Applied || await HasMissingSnapshotsAsync(context, cancellationToken))
            throw new InvalidOperationException("Reporting startup migration did not capture every active report. Startup is blocked.");

        await transaction.CommitAsync(cancellationToken);
    }

    private static Task<bool> HasMissingSnapshotsAsync(AppDbContext context, CancellationToken cancellationToken)
        => context.ReportRequests.IgnoreQueryFilters().AsNoTracking().AnyAsync(
            request => !request.IsDeleted && !context.ReportRequestDefinitionSnapshots.IgnoreQueryFilters()
                .Any(snapshot => snapshot.ReportRequestId == request.Id && !snapshot.IsDeleted), cancellationToken);

    private static void ThrowIfBlocked(ReportingPhotoTypesMigrationReport report)
    {
        var errors = report.Exceptions.Where(issue => issue.Severity == "Error").ToArray();
        if (errors.Length > 0)
            throw new InvalidOperationException($"Reporting startup migration blocked ({errors.Length} issues): {string.Join(", ", errors.Select(issue => issue.Code).Distinct())}. No API or workers were started.");
    }
}
