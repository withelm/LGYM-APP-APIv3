using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Api.Features.Training.Contracts;
using LgymApi.Api.Features.Enum;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class ExerciseScoreProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<ExerciseScore, ExerciseScoreResponseDto>((source, _) => new ExerciseScoreResponseDto
        {
            Id = source.Id.ToString(),
            ExerciseId = source.ExerciseId.ToString(),
            Reps = source.Reps,
            Series = source.Series,
            Weight = source.Weight,
            Unit = source.Unit.ToLookup()
        });

        configuration.CreateMap<ExerciseScore, ScoreDto>((source, _) => new ScoreDto
        {
            Id = source.Id.ToString(),
            Reps = source.Reps,
            Weight = source.Weight,
            Unit = source.Unit.ToLookup()
        });

        configuration.CreateMap<ExerciseScore, ScoreWithGymDto>((source, _) => new ScoreWithGymDto
        {
            Id = source.Id.ToString(),
            Reps = source.Reps,
            Weight = source.Weight,
            Unit = source.Unit.ToLookup(),
            GymName = source.Training?.Gym?.Name
        });
    }
}
