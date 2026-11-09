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
    public async Task<ExerciseMediaSession?> FindAsync(Id<ExerciseMediaUpload> id, CancellationToken cancellationToken)
    {
        var entity = await context.ExerciseMediaUploads.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : mapper.Map<ExerciseMediaUpload, ExerciseMediaSession>(entity);
    }
    public async Task<ExerciseMediaSession?> FindOperationAsync(Id<AccountReference> actorId, Id<ExerciseMediaOperationReference> operationId, CancellationToken cancellationToken)
    {
        var persistedActor = WorkoutPersistenceAccountIds.ToPersisted(actorId);
        var entity = await context.ExerciseMediaUploads.AsNoTracking().FirstOrDefaultAsync(x => x.ActorId == persistedActor && x.OperationId == operationId, cancellationToken);
        return entity is null ? null : mapper.Map<ExerciseMediaUpload, ExerciseMediaSession>(entity);
    }
    public Task<int> CountRecentAsync(Id<AccountReference> actorId, DateTimeOffset since, CancellationToken cancellationToken)
    {
        var persistedActor = WorkoutPersistenceAccountIds.ToPersisted(actorId);
        return context.ExerciseMediaUploads.CountAsync(x => x.ActorId == persistedActor && x.CreatedAt >= since, cancellationToken);
    }
    public Task AddAsync(ExerciseMediaSession session, CancellationToken cancellationToken) => context.ExerciseMediaUploads.AddAsync(mapper.Map<ExerciseMediaSession, ExerciseMediaUpload>(session), cancellationToken).AsTask();
    public Task SaveSessionRevisionAsync(ExerciseMediaSession session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        session.Revision++;
        var entity = context.ExerciseMediaUploads.Local.Single(x => x.Id == session.Id);
        var values = mapper.Map<ExerciseMediaSession, ExerciseMediaUpload>(session);
        // Existing audit timestamps are owned by the shared save pipeline, not by session DTOs.
        values.CreatedAt = entity.CreatedAt;
        values.UpdatedAt = entity.UpdatedAt;
        context.Entry(entity).CurrentValues.SetValues(values);
        return Task.CompletedTask;
    }

    public Task StageImageAsync(Id<Exercise> exerciseId, string? expectedImage, string? newImage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = new Exercise { Id = exerciseId, Image = newImage };
        var entry = context.Exercises.Attach(entity);
        entry.Property(x => x.Image).OriginalValue = expectedImage;
        entry.Property(x => x.Image).IsModified = true;
        // UserId=null and IsDeleted=false are additional atomic eligibility preconditions.
        return Task.CompletedTask;
    }

    public Task<bool> WasRetainedAsync(Id<Exercise> exerciseId, string image, CancellationToken cancellationToken) => context.ExerciseMediaUploads.AsNoTracking().AnyAsync(x => x.TargetExerciseId == exerciseId && x.Status == "bound" && (x.PreviousImage == image || x.ResultImage == image), cancellationToken);

    public async Task<IReadOnlyList<ExerciseMediaSession>> CleanupCandidatesAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var entities = await context.ExerciseMediaUploads.Where(x => !x.PrivateObjectsCleaned && x.ExpiresAt < now).OrderBy(x => x.ExpiresAt).Take(100).ToListAsync(cancellationToken);
        return mapper.MapList<ExerciseMediaUpload, ExerciseMediaSession>(entities);
    }
    public bool IsConcurrencyFailure(Exception exception) => exception is DbUpdateConcurrencyException || exception is DbUpdateException { InnerException: Npgsql.PostgresException { SqlState: "23505", ConstraintName: "IX_ExerciseMediaUploads_ActorId_OperationId" } };
}
