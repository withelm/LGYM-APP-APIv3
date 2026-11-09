using LgymApi.Api.Middleware;
using LgymApi.Domain.ValueObjects;
using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class ExerciseMediaProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<InitiateExerciseMediaDto, InitiateExerciseMediaInput>((s, _) => new(s.ContentType, s.SizeBytes, s.Sha256, s.ExerciseId is null ? null : s.ExerciseId.ToIdOrEmpty<Exercise>()));
        configuration.CreateMap<CreateExerciseMediaDto, CreateExerciseMediaInput>((s, _) => new(s.OperationId.ToIdOrEmpty<ExerciseMediaOperationReference>(), s.Name, s.BodyPart, string.IsNullOrWhiteSpace(s.EloFormula?.Id) ? null : Enum.Parse<ExerciseEloFormula>(s.EloFormula.Id, true), s.Description, s.UploadReference is null ? null : s.UploadReference.ToIdOrEmpty<ExerciseMediaUpload>()));
        configuration.CreateMap<ChangeExerciseMediaDto, ChangeExerciseMediaInput>((s, _) => new(s.OperationId.ToIdOrEmpty<ExerciseMediaOperationReference>(), s.ExpectedImage, s.UploadReference is null ? null : s.UploadReference.ToIdOrEmpty<ExerciseMediaUpload>(), s.Remove, s.RestoreImage));
        configuration.CreateMap<ExerciseMediaUploadResult, ExerciseMediaUploadDto>((s, _) => new() { UploadReference = s.UploadReference, UploadUrl = s.UploadUrl, ExpiresAt = s.ExpiresAt, Headers = s.Headers });
        configuration.CreateMap<ExerciseMediaVerificationResult, ExerciseMediaVerificationDto>((s, _) => new() { UploadReference = s.UploadReference, Status = s.Status, SourceSha256 = s.SourceSha256, PublishedSha256 = s.PublishedSha256, ContentType = s.ContentType, Width = s.Width, Height = s.Height, Frames = s.Frames });
        configuration.CreateMap<ExerciseMediaMutationResult, ExerciseMediaMutationDto>((s, _) => new() { Id = s.Id, Image = s.Image, Status = s.Status });
        configuration.CreateMap<ExerciseMediaCleanupResult, ExerciseMediaCleanupDto>((s, _) => new() { Cleaned = s.Cleaned, Remaining = s.Remaining });
    }
}
