using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class ExerciseMediaProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<ExerciseMediaUploadResult, ExerciseMediaUploadResponseDto>((source, _) => new() { Image = source.Image });
    }
}
