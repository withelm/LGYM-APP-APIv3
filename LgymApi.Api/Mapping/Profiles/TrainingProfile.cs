using LgymApi.Api.Features.PlanDay.Contracts;
using LgymApi.Api.Features.Training.Contracts;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Api.Features.Enum;
using LgymApi.Application.Features.Training.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class TrainingProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<Training, LastTrainingInfoDto>((source, _) => new LastTrainingInfoDto
        {
            Id = source.Id.ToString(),
            TypePlanDayId = source.TypePlanDayId.ToString(),
            CreatedAt = source.CreatedAt.UtcDateTime,
            PlanDay = source.PlanDay == null
                ? new PlanDayChooseDto()
                : new PlanDayChooseDto
                {
                    Id = source.PlanDay.Id.ToString(),
                    Name = source.PlanDay.Name
                }
        });

        configuration.CreateMap<TrainingByDateDetails, TrainingByDateDetailsDto>((source, _) => new TrainingByDateDetailsDto
        {
            Id = source.Id.ToString(),
            TypePlanDayId = source.TypePlanDayId.ToString(),
            CreatedAt = source.CreatedAt,
            PlanDay = source.PlanDay == null
                ? new PlanDayChooseDto()
                : new PlanDayChooseDto
                {
                    Id = source.PlanDay.Id.ToString(),
                    Name = source.PlanDay.Name
                },
            Gym = source.Gym,
            Exercises = source.Exercises.Select(exercise => new EnrichedExerciseDto
            {
                ExerciseScoreId = exercise.ExerciseScoreId.ToString(),
                ExerciseDetails = new ExerciseResponseDto
                {
                    Id = exercise.ExerciseDetails.Id.ToString(),
                    Name = exercise.ExerciseDetails.Name,
                    BodyPart = exercise.ExerciseDetails.BodyPart.ToLookup(),
                    Description = exercise.ExerciseDetails.Description,
                    Image = exercise.ExerciseDetails.Image,
                    UserId = exercise.ExerciseDetails.UserId?.ToString()
                },
                ScoresDetails = exercise.ScoresDetails.Select(score => new ExerciseScoreResponseDto
                {
                    Id = score.Id.ToString(),
                    ExerciseId = score.ExerciseId.ToString(),
                    Reps = score.Reps,
                    Series = score.Series,
                    Weight = score.Weight,
                    Unit = score.Unit.ToLookup()
                }).ToList()
            }).ToList()
        });
    }
}
