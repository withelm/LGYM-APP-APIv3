using LgymApi.Api.DTOs;
using LgymApi.Api.Services;
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
            var exerciseDetails = new ExerciseResponseDto();

            var exerciseMap = context?.Get(Keys.ExerciseMap);
            if (exerciseMap != null && exerciseMap.TryGetValue(source.ExerciseId, out var exercise))
            {
                exerciseDetails = new ExerciseResponseDto
                {
                    Id = exercise.Id.ToString(),
                    Name = exercise.Name,
                    BodyPart = exercise.BodyPart.ToLookup(),
                    Description = exercise.Description,
                    Image = exercise.Image,
                    UserId = exercise.UserId?.ToString()
                };
            }

            return new MainRecordsLastDto
            {
                Id = source.Id.ToString(),
                ExerciseId = source.ExerciseId.ToString(),
                Weight = source.Weight,
                Unit = source.Unit.ToLookup(),
                Date = source.Date.UtcDateTime,
                ExerciseDetails = exerciseDetails
            };
        });
    }
}
