using LgymApi.Api.Features.Enum;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Api.Features.MainRecords.Contracts;
using LgymApi.Application.Features.MainRecords.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class MainRecordProfile : IMappingProfile
{
    internal static class Keys
    {
        internal static readonly ContextKey<IReadOnlyDictionary<Guid, Exercise>> ExerciseMap = new("MainRecord.ExerciseMap");
    }

    public void Configure(MappingConfiguration configuration)
    {
        configuration.AllowContextKey(Keys.ExerciseMap);

        configuration.CreateMap<MainRecord, MainRecordResponseDto>((source, _) => new MainRecordResponseDto
        {
            Id = source.Id.ToString(),
            ExerciseId = source.ExerciseId.ToString(),
            Weight = source.Weight,
            Unit = source.Unit.ToLookup(),
            Date = source.Date.UtcDateTime
        });

        configuration.CreateMap<MainRecord, MainRecordsLastDto>((source, context) =>
        {
            var exerciseMap = context?.Get(Keys.ExerciseMap);
            var exercise = exerciseMap != null && exerciseMap.TryGetValue(source.ExerciseId, out var resolvedExercise)
                ? resolvedExercise
                : null;

            return new MainRecordsLastDto
            {
                Id = source.Id.ToString(),
                ExerciseId = source.ExerciseId.ToString(),
                Weight = source.Weight,
                Unit = source.Unit.ToLookup(),
                Date = source.Date.UtcDateTime,
                ExerciseDetails = exercise == null
                    ? new ExerciseResponseDto()
                    : context!.Map<Exercise, ExerciseResponseDto>(exercise)
            };
        });

        configuration.CreateMap<PossibleRecordResult, PossibleRecordForExerciseDto>((source, _) => new PossibleRecordForExerciseDto
        {
            Weight = source.Weight,
            Reps = source.Reps,
            Unit = source.Unit.ToLookup(),
            Date = source.Date
        });
    }
}
