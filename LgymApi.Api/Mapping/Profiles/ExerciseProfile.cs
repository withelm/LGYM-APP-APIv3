using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Api.Features.Enum;
using LgymApi.Application.Features.Exercise.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class ExerciseProfile : IMappingProfile
{
    internal static class Keys
    {
        internal static readonly ContextKey<IReadOnlyDictionary<Guid, string>> Translations = new("Exercise.Translations");
    }

    public void Configure(MappingConfiguration configuration)
    {
        configuration.AllowContextKey(Keys.Translations);

        configuration.CreateMap<Exercise, ExerciseResponseDto>((source, context) =>
        {
            var name = source.Name;

            if (source.UserId is null)
            {
                var translations = context?.Get(Keys.Translations);
                if (translations != null && translations.TryGetValue(source.Id, out var translatedName))
                {
                    name = translatedName;
                }
            }

            return new ExerciseResponseDto
            {
                Id = source.Id.ToString(),
                Name = name,
                BodyPart = source.BodyPart.ToLookup(),
                Description = source.Description,
                Image = source.Image,
                UserId = source.UserId?.ToString()
            };
        });

        configuration.CreateMap<SeriesScoreResult, SeriesScoreWithGymDto>((source, context) => new SeriesScoreWithGymDto
        {
            Series = source.Series,
            Score = source.Score == null ? null : context!.Map<ExerciseScore, ScoreWithGymDto>(source.Score)
        });

        configuration.CreateMap<LastExerciseScoresResult, LastExerciseScoresResponseDto>((source, context) => new LastExerciseScoresResponseDto
        {
            ExerciseId = source.ExerciseId.ToString(),
            ExerciseName = source.ExerciseName,
            SeriesScores = context!.MapList<SeriesScoreResult, SeriesScoreWithGymDto>(source.SeriesScores)
        });

        configuration.CreateMap<SeriesScoreResult, SeriesScoreDto>((source, context) => new SeriesScoreDto
        {
            Series = source.Series,
            Score = source.Score == null ? null : context!.Map<ExerciseScore, ScoreDto>(source.Score)
        });

        configuration.CreateMap<ExerciseTrainingHistoryItem, ExerciseTrainingHistoryItemDto>((source, context) => new ExerciseTrainingHistoryItemDto
        {
            Id = source.Id.ToString(),
            Date = source.Date,
            GymName = source.GymName,
            TrainingName = source.TrainingName,
            SeriesScores = context!.MapList<SeriesScoreResult, SeriesScoreDto>(source.SeriesScores)
        });
    }
}
