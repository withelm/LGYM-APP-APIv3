using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using LgymApi.Application.Mapping.Core;
using LgymApi.Identity.Contracts;

namespace LgymApi.Infrastructure.Repositories.WorkoutProgress;

public sealed class ExerciseMediaPersistence(AppDbContext context, IMapper mapper) : IExerciseMediaPersistence
{
    public async Task<ExerciseMediaSession?> FindOperationAsync(Id<AccountReference> actorId, Id<ExerciseMediaOperationReference> operationId, CancellationToken cancellationToken)
    {
        var persistedActor = WorkoutPersistenceAccountIds.ToPersisted(actorId);
        var entity = await context.ExerciseMediaUploads.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.ActorId == persistedActor && x.OperationId == operationId, cancellationToken);
        return entity is null ? null : mapper.Map<ExerciseMediaUpload, ExerciseMediaSession>(entity);
    }
    public Task AddAsync(ExerciseMediaSession session, CancellationToken cancellationToken) => context.ExerciseMediaUploads.AddAsync(mapper.Map<ExerciseMediaSession, ExerciseMediaUpload>(session), cancellationToken).AsTask();
    public Task SaveSessionRevisionAsync(ExerciseMediaSession session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        session.Revision++;
        var entity = context.ExerciseMediaUploads.Local.Single(x => x.Id == session.Id);
        var values = mapper.Map<ExerciseMediaSession, ExerciseMediaUpload>(session);
        values.CreatedAt = entity.CreatedAt;
        values.UpdatedAt = entity.UpdatedAt;
        context.Entry(entity).CurrentValues.SetValues(values);
        return Task.CompletedTask;
    }
    public async Task<IReadOnlyList<ExerciseMediaSession>> ExpiredOperationsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var entities = await context.ExerciseMediaUploads.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Status == "publishing" && x.ExpiresAt <= now)
            .OrderBy(x => x.ExpiresAt).ThenBy(x => x.CreatedAt).Take(100).ToListAsync(cancellationToken);
        return mapper.MapList<ExerciseMediaUpload, ExerciseMediaSession>(entities);
    }
    public Task StageImageAsync(Id<Exercise> exerciseId, string? expectedImage, string? newImage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = new Exercise { Id = exerciseId, Image = newImage };
        var entry = context.Exercises.Attach(entity);
        entry.Property(x => x.Image).OriginalValue = expectedImage;
        entry.Property(x => x.Image).IsModified = true;
        return Task.CompletedTask;
    }
    public Task<bool> WasRetainedAsync(Id<Exercise> exerciseId, string image, CancellationToken cancellationToken) => context.ExerciseMediaUploads.IgnoreQueryFilters().AsNoTracking().AnyAsync(x => x.TargetExerciseId == exerciseId && x.Status == "bound" && (x.PreviousImage == image || x.ResultImage == image), cancellationToken);
    public async Task<ExerciseMediaObjectState?> FindObjectAsync(string objectKey, CancellationToken cancellationToken)
    {
        var entity = await context.ExerciseMediaObjectFences.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.ObjectKey == objectKey, cancellationToken);
        return entity is null ? null : mapper.Map<ExerciseMediaObjectFence, ExerciseMediaObjectState>(entity);
    }
    public Task AddObjectAsync(ExerciseMediaObjectState state, CancellationToken cancellationToken) => context.ExerciseMediaObjectFences.AddAsync(mapper.Map<ExerciseMediaObjectState, ExerciseMediaObjectFence>(state), cancellationToken).AsTask();
    public Task SaveObjectRevisionAsync(ExerciseMediaObjectState state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        state.Revision++;
        var entity = context.ExerciseMediaObjectFences.Local.Single(x => x.Id == state.Id);
        var values = mapper.Map<ExerciseMediaObjectState, ExerciseMediaObjectFence>(state);
        values.CreatedAt = entity.CreatedAt;
        values.UpdatedAt = entity.UpdatedAt;
        context.Entry(entity).CurrentValues.SetValues(values);
        return Task.CompletedTask;
    }
    public async Task<IReadOnlyList<ExerciseMediaObjectState>> CleanupCandidatesAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var before = now.AddHours(-24);
        var entities = await context.ExerciseMediaObjectFences.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.IsRetired || x.CreatedAt < before)
            .Where(x => !context.Exercises.IgnoreQueryFilters().Any(exercise => exercise.Image == x.PublicUrl))
            .Where(x => !context.ExerciseMediaUploads.IgnoreQueryFilters().Any(operation => operation.Status == "bound" && (operation.ObjectKey == x.ObjectKey || operation.PreviousImage == x.PublicUrl || operation.ResultImage == x.PublicUrl)))
            .Where(x => !context.ExerciseMediaUploads.IgnoreQueryFilters().Any(operation => operation.ObjectKey == x.ObjectKey
                && (operation.Status != "publishing" && operation.Status != "rejected"
                    || operation.Status == "publishing" && operation.ExpiresAt > now)))
            .OrderBy(x => x.LastCleanupAt ?? DateTimeOffset.MinValue).ThenBy(x => x.CreatedAt).Take(100).ToListAsync(cancellationToken);
        return mapper.MapList<ExerciseMediaObjectFence, ExerciseMediaObjectState>(entities);
    }
    public async Task<IReadOnlyList<ExerciseMediaSession>> SessionsForObjectAsync(string objectKey, CancellationToken cancellationToken)
    {
        var entities = await context.ExerciseMediaUploads.IgnoreQueryFilters().Where(x => x.ObjectKey == objectKey).ToListAsync(cancellationToken);
        return mapper.MapList<ExerciseMediaUpload, ExerciseMediaSession>(entities);
    }
    public async Task<bool> IsImageReferencedAsync(string image, CancellationToken cancellationToken) =>
        await context.Exercises.IgnoreQueryFilters().AsNoTracking().AnyAsync(x => x.Image == image, cancellationToken)
        || await context.ExerciseMediaUploads.IgnoreQueryFilters().AsNoTracking().AnyAsync(x => x.Status == "bound" && (x.PreviousImage == image || x.ResultImage == image), cancellationToken);
    public void DiscardTrackedState()
    {
        foreach (var entry in context.ChangeTracker.Entries<ExerciseMediaUpload>().ToList()) entry.State = EntityState.Detached;
        foreach (var entry in context.ChangeTracker.Entries<ExerciseMediaObjectFence>().ToList()) entry.State = EntityState.Detached;
    }
    public bool IsConcurrencyFailure(Exception exception) => exception is DbUpdateConcurrencyException || exception is DbUpdateException { InnerException: Npgsql.PostgresException { SqlState: "23505", ConstraintName: "IX_ExerciseMediaUploads_ActorId_OperationId" or "IX_ExerciseMediaObjectFences_ObjectKey" } };
}
