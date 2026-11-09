using LgymApi.Application.Mapping.Core;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.Domain.Entities;

namespace LgymApi.Infrastructure.Repositories.WorkoutProgress;

internal sealed class ExerciseMediaPersistenceProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<ExerciseMediaUpload, ExerciseMediaSession>((source, _) => new()
        {
            ActorId = WorkoutPersistenceAccountIds.ToContract(source.ActorId),
            Id = source.Id,
            TargetExerciseId = source.TargetExerciseId,
            ContentType = source.ContentType,
            SizeBytes = source.SizeBytes,
            SourceSha256 = source.SourceSha256,
            PublishedSha256 = source.PublishedSha256,
            Extension = source.Extension,
            Width = source.Width,
            Height = source.Height,
            Frames = source.Frames,
            ExpiresAt = source.ExpiresAt,
            Status = source.Status,
            Revision = source.Revision,
            OperationId = source.OperationId,
            OperationHash = source.OperationHash,
            PreviousImage = source.PreviousImage,
            ResultImage = source.ResultImage,
            PrivateObjectsCleaned = source.PrivateObjectsCleaned,
        });
        configuration.CreateMap<ExerciseMediaSession, ExerciseMediaUpload>((source, _) => new()
        {
            ActorId = WorkoutPersistenceAccountIds.ToPersisted(source.ActorId),
            Id = source.Id,
            TargetExerciseId = source.TargetExerciseId,
            ContentType = source.ContentType,
            SizeBytes = source.SizeBytes,
            SourceSha256 = source.SourceSha256,
            PublishedSha256 = source.PublishedSha256,
            Extension = source.Extension,
            Width = source.Width,
            Height = source.Height,
            Frames = source.Frames,
            ExpiresAt = source.ExpiresAt,
            Status = source.Status,
            Revision = source.Revision,
            OperationId = source.OperationId,
            OperationHash = source.OperationHash,
            PreviousImage = source.PreviousImage,
            ResultImage = source.ResultImage,
            PrivateObjectsCleaned = source.PrivateObjectsCleaned,
        });
    }
}