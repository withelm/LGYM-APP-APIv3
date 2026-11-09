using LgymApi.Api.Extensions;
using LgymApi.Api.Middleware;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class ExerciseMediaProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<CreateExerciseMediaDto, CreateExerciseMediaInput>((s, _) => new(s.OperationId.ToIdOrEmpty<ExerciseMediaOperationReference>(), s.Name, s.BodyPart, s.EloFormulaId is null ? null : Enum.Parse<ExerciseEloFormula>(s.EloFormulaId, true), s.Description, Source(s.ContentType, s.SizeBytes, s.SourceSha256)));
        configuration.CreateMap<ChangeExerciseMediaDto, ChangeExerciseMediaInput>((s, _) => new(s.OperationId.ToIdOrEmpty<ExerciseMediaOperationReference>(), s.ExpectedImage, Source(s.ContentType, s.SizeBytes, s.SourceSha256), s.Remove, s.RestoreImage));
        configuration.CreateMap<ExerciseMediaMutationResult, ExerciseMediaMutationDto>((s, _) => new() { Id = s.Id, Image = s.Image, Status = s.Status });
        configuration.CreateMap<ExerciseMediaFailureResult, ExerciseMediaErrorDto>((s, _) => new() { OperationId = s.OperationId, Message = s.Message, Disposition = s.Disposition });
        configuration.CreateMap<ExerciseMediaCleanupResult, ExerciseMediaCleanupDto>((s, _) => new() { Cleaned = s.Cleaned, Remaining = s.Remaining });
    }

    private static ExerciseMediaSourceInput? Source(string? contentType, long? sizeBytes, string? sha256) => sha256 is null ? null : new(contentType!, sizeBytes!.Value, sha256.ToLowerInvariant());
}
