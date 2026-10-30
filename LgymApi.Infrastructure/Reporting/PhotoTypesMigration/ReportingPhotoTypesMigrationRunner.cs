using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Reporting.PhotoTypesMigration;

public sealed class ReportingPhotoTypesMigrationRunner(AppDbContext context)
{
    public async Task<ReportingPhotoTypesMigrationReport> RunAsync(
        ReportingPhotoTypesMigrationOptions options,
        CancellationToken cancellationToken)
    {
        var capturedAtUtc = DateTimeOffset.UtcNow;
        var state = await ReportingPhotoTypesMigrationState.LoadAsync(context, cancellationToken);
        var plan = new ReportingPhotoTypesMigrationPlanner().Create(state, options, capturedAtUtc);
        var report = plan.Report;

        if (options.Mode == ReportingPhotoTypesMigrationMode.DryRun)
        {
            return report;
        }

        if (!options.ExternalFenceAcknowledged)
        {
            AddGlobalError(report, "EXTERNAL_FENCE_REQUIRED", "migration", "Apply requires explicit confirmation that every old writer and transaction has stopped.");
            return report;
        }

        if (!string.Equals(options.ExpectedFingerprint, plan.Fingerprint, StringComparison.OrdinalIgnoreCase))
        {
            AddGlobalError(report, "FINGERPRINT_CHANGED", "migration", "The source definition fingerprint differs from dry-run; rerun dry-run and investigate the change.");
            return report;
        }

        if (report.HasGlobalBlockers)
        {
            return report;
        }

        report.Applied = true;
        if (!await ApplyPhotoTypesAsync(plan, options.BatchSize, report, cancellationToken))
        {
            return report;
        }

        await ApplyRequestsAsync(plan, options.BatchSize, report, cancellationToken);
        return report;
    }

    private async Task<bool> ApplyPhotoTypesAsync(
        ReportingPhotoTypesMigrationPlan plan,
        int batchSize,
        ReportingPhotoTypesMigrationReport report,
        CancellationToken cancellationToken)
    {
        foreach (var batch in plan.PhotoTypes.Chunk(batchSize))
        {
            await using var transaction = context.Database.CurrentTransaction is null
                ? await context.Database.BeginTransactionAsync(cancellationToken) : null;
            try
            {
                foreach (var planned in batch)
                {
                    var entity = planned.Entity;
                    var exists = await context.TrainerPhotoTypes
                        .IgnoreQueryFilters()
                        .AnyAsync(photoType => photoType.Id == entity.Id, cancellationToken);
                    if (exists)
                    {
                        continue;
                    }

                    entity.CreatedAt = plan.CapturedAtUtc;
                    await context.TrainerPhotoTypes.AddAsync(entity, cancellationToken);
                }

                report.Counters.DefaultsWritten += await context.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                    report.Counters.BatchesCommitted++;
                }
                context.ChangeTracker.Clear();
            }
            catch (Exception exception)
            {
                if (transaction is null) throw;
                await transaction.RollbackAsync(cancellationToken);
                context.ChangeTracker.Clear();
                AddGlobalError(
                    report,
                    "DEFAULT_BATCH_FAILED",
                    batch[0].Entity.Id.ToString(),
                    $"A default-definition checkpoint failed with {exception.GetType().Name}; the transaction was rolled back and the run stopped.");
                return false;
            }
        }

        return true;
    }

    private async Task ApplyRequestsAsync(
        ReportingPhotoTypesMigrationPlan plan,
        int batchSize,
        ReportingPhotoTypesMigrationReport report,
        CancellationToken cancellationToken)
    {
        foreach (var batch in plan.Requests.Chunk(batchSize))
        {
            await using var transaction = context.Database.CurrentTransaction is null
                ? await context.Database.BeginTransactionAsync(cancellationToken) : null;
            var photosMapped = 0;
            var snapshotsWritten = 0;
            var existingSnapshotsPreserved = 0;
            try
            {
                foreach (var requestPlan in batch)
                {
                    var snapshotExists = await context.ReportRequestDefinitionSnapshots
                        .IgnoreQueryFilters()
                        .AnyAsync(snapshot => snapshot.ReportRequestId == requestPlan.ReportRequestId, cancellationToken);
                    if (snapshotExists)
                    {
                        existingSnapshotsPreserved++;
                        continue;
                    }

                    foreach (var binding in requestPlan.PhotoBindings)
                    {
                        var updated = await context.Photos
                            .IgnoreQueryFilters()
                            .Where(photo => photo.Id == binding.PhotoId
                                            && !photo.IsDeleted
                                            && photo.SourcePhotoTypeId == null
                                            && photo.ReportPhotoSlotId == null)
                            .StageUpdateAsync(context,
                                photo => photo.SourcePhotoTypeId, photo => binding.SourcePhotoTypeId,
                                photo => photo.UpdatedAt, photo => photo.UpdatedAt,
                                cancellationToken);
                        if (updated != 1)
                        {
                            throw new InvalidOperationException("A planned photo association changed after dry-run.");
                        }
                        // Explicit timestamp self-assignment prevents the shared helper from stamping
                        // historical rows. Both association updates commit in this checkpoint transaction.
                        var slotUpdated = await context.Photos.IgnoreQueryFilters()
                            .Where(photo => photo.Id == binding.PhotoId && !photo.IsDeleted
                                && photo.SourcePhotoTypeId == binding.SourcePhotoTypeId && photo.ReportPhotoSlotId == null)
                            .StageUpdateAsync(context,
                                photo => photo.ReportPhotoSlotId, photo => binding.ReportPhotoSlotId,
                                photo => photo.UpdatedAt, photo => photo.UpdatedAt, cancellationToken);
                        if (slotUpdated != 1)
                            throw new InvalidOperationException("A planned photo slot changed during the checkpoint.");
                        photosMapped++;
                    }

                    await context.ReportRequestDefinitionSnapshots.AddAsync(requestPlan.Snapshot, cancellationToken);
                    snapshotsWritten++;
                }

                await context.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                report.Counters.PhotosMapped += photosMapped;
                report.Counters.SnapshotsWritten += snapshotsWritten;
                report.Counters.ExistingSnapshotsPreserved += existingSnapshotsPreserved;
                if (transaction is not null) report.Counters.BatchesCommitted++;
                context.ChangeTracker.Clear();
            }
            catch (Exception exception)
            {
                if (transaction is null) throw;
                await transaction.RollbackAsync(cancellationToken);
                context.ChangeTracker.Clear();
                AddGlobalError(
                    report,
                    "REQUEST_BATCH_FAILED",
                    batch[0].ReportRequestId.ToString(),
                    $"A request checkpoint failed with {exception.GetType().Name}; the transaction was rolled back and the run stopped.");
                return;
            }
        }
    }

    private static void AddGlobalError(
        ReportingPhotoTypesMigrationReport report,
        string code,
        string entityId,
        string detail)
        => report.Exceptions.Add(new ReportingPhotoTypesMigrationIssue(
            "Error",
            "Global",
            code,
            entityId,
            detail));
}
