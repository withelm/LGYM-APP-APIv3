using LgymApi.Api.DTOs;
using LgymApi.Api.Services;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class PlanDayProfile : IMappingProfile
{
    private const string ExerciseMapContextKey = "exerciseMap";
    private const string ExercisesContextKey = "planDayExercises";
    private const string LastTrainingMapContextKey = "planDayLastTrainings";

    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<PlanDay, PlanDayChooseDto>((source, _) => new PlanDayChooseDto
        {
            Id = source.Id.ToString(),
            Name = source.Name
        });

        configuration.CreateMap<PlanDay, PlanDayVmDto>((source, context) =>
        {
            var exercises = context?.Get<IReadOnlyList<PlanDayExercise>>(ExercisesContextKey) ?? Array.Empty<PlanDayExercise>();
            var exerciseMap = context?.Get<IReadOnlyDictionary<Guid, Exercise>>(ExerciseMapContextKey);

            var vmExercises = exercises
                .Where(e => e.PlanDayId == source.Id)
                .Select(exercise => new PlanDayExerciseVmDto
                {
                    Series = exercise.Series,
                    Reps = exercise.Reps,
                    Exercise = ResolveExerciseDto(exercise, exerciseMap)
                })
                .ToList();

            return new PlanDayVmDto
            {
                Id = source.Id.ToString(),
                Name = source.Name,
                Exercises = vmExercises
            };
        });

        configuration.CreateMap<PlanDay, PlanDayBaseInfoDto>((source, context) =>
        {
            var exercises = context?.Get<IReadOnlyList<PlanDayExercise>>(ExercisesContextKey) ?? Array.Empty<PlanDayExercise>();
            var lastTrainingMap = context?.Get<IReadOnlyDictionary<Guid, DateTime?>>(LastTrainingMapContextKey);

            var filteredExercises = exercises.Where(e => e.PlanDayId == source.Id).ToList();
            var lastTrainingDate = lastTrainingMap != null && lastTrainingMap.TryGetValue(source.Id, out var date)
                ? date
                : null;

            return new PlanDayBaseInfoDto
            {
                Id = source.Id.ToString(),
                Name = source.Name,
                LastTrainingDate = lastTrainingDate,
                TotalNumberOfSeries = filteredExercises.Sum(e => e.Series),
                TotalNumberOfExercises = filteredExercises.Count
            };
        });
    }

    private static ExerciseResponseDto ResolveExerciseDto(PlanDayExercise exercise, IReadOnlyDictionary<Guid, Exercise>? exerciseMap)
    {
        if (exerciseMap != null && exerciseMap.TryGetValue(exercise.ExerciseId, out var entity))
        {
            return new ExerciseResponseDto
            {
                Id = entity.Id.ToString(),
                Name = entity.Name,
                BodyPart = entity.BodyPart.ToLookup(),
                Description = entity.Description,
                Image = entity.Image,
                UserId = entity.UserId?.ToString()
            };
        }

        return new ExerciseResponseDto();
    }
}
