using LgymApi.Application.Mapping.Core;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ExerciseMedia;

namespace LgymApi.Infrastructure.Repositories.WorkoutProgress;

internal sealed class ExerciseMediaPersistenceProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<ExerciseMediaUpload, ExerciseMediaSession>((source, _) => CopyState(source, new ExerciseMediaSession
        {
            ActorId = WorkoutPersistenceAccountIds.ToContract(source.ActorId),
            Id = source.Id,
        }));
        configuration.CreateMap<ExerciseMediaSession, ExerciseMediaUpload>((source, _) => CopyState(source, new ExerciseMediaUpload
        {
            ActorId = WorkoutPersistenceAccountIds.ToPersisted(source.ActorId),
            Id = source.Id,
        }));
        configuration.CreateMap<ExerciseMediaObjectFence, ExerciseMediaObjectState>((source, _) => new()
        {
            Id = source.Id, ExerciseId = source.ExerciseId, ObjectKey = source.ObjectKey,
            PublicUrl = source.PublicUrl, Revision = source.Revision, IsRetired = source.IsRetired,
            LastCleanupAt = source.LastCleanupAt, LastDeleteSucceeded = source.LastDeleteSucceeded
        });
        configuration.CreateMap<ExerciseMediaObjectState, ExerciseMediaObjectFence>((source, _) => new()
        {
            Id = source.Id, ExerciseId = source.ExerciseId, ObjectKey = source.ObjectKey,
            PublicUrl = source.PublicUrl, Revision = source.Revision, IsRetired = source.IsRetired,
            LastCleanupAt = source.LastCleanupAt, LastDeleteSucceeded = source.LastDeleteSucceeded
        });
    }

    private static TTarget CopyState<TTarget>(IExerciseMediaState source, TTarget target) where TTarget : class, IExerciseMediaState
    {
        target.TargetExerciseId = source.TargetExerciseId;
        target.ContentType = source.ContentType;
        target.SizeBytes = source.SizeBytes;
        target.SourceSha256 = source.SourceSha256;
        target.PublishedSha256 = source.PublishedSha256;
        target.Extension = source.Extension;
        target.Width = source.Width;
        target.Height = source.Height;
        target.Frames = source.Frames;
        target.ExpiresAt = source.ExpiresAt;
        target.Status = source.Status;
        target.Revision = source.Revision;
        target.OperationId = source.OperationId;
        target.OperationHash = source.OperationHash;
        target.PreviousImage = source.PreviousImage;
        target.ResultImage = source.ResultImage;
        target.IsCreate = source.IsCreate;
        target.ObjectKey = source.ObjectKey;
        target.RejectionStatusCode = source.RejectionStatusCode;
        return target;
    }
}
