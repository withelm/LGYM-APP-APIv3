using LgymApi.Api.Features.PlanDay.Contracts;
using LgymApi.Api.Features.Training.Contracts;
using LgymApi.Api.Features.User.Contracts;
using LgymApi.Api.Features.Enum;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Application.Features.Training.Models;
using LgymApi.Application.Features.User.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class TrainingProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<ScoreResult, ScoreResultDto>((source, _) => new ScoreResultDto
        {
            Reps = source.Reps,
            Weight = source.Weight,
            Unit = source.Unit.ToLookup()
        });

        configuration.CreateMap<SeriesComparison, SeriesComparisonDto>((source, context) => new SeriesComparisonDto
        {
            Series = source.Series,
            CurrentResult = context!.Map<ScoreResult, ScoreResultDto>(source.CurrentResult),
            PreviousResult = source.PreviousResult == null
                ? null
                : context!.Map<ScoreResult, ScoreResultDto>(source.PreviousResult)
        });

        configuration.CreateMap<GroupedExerciseComparison, GroupedExerciseComparisonDto>((source, context) => new GroupedExerciseComparisonDto
        {
            ExerciseId = source.ExerciseId.ToString(),
            ExerciseName = source.ExerciseName,
            SeriesComparisons = context!.MapList<SeriesComparison, SeriesComparisonDto>(source.SeriesComparisons)
        });

        configuration.CreateMap<TrainingSummaryResult, TrainingSummaryDto>((source, context) => new TrainingSummaryDto
        {
            Comparison = context!.MapList<GroupedExerciseComparison, GroupedExerciseComparisonDto>(source.Comparison),
            GainElo = source.GainElo,
            UserOldElo = source.UserOldElo,
            ProfileRank = source.ProfileRank == null ? null : context!.Map<RankInfo, RankDto>(source.ProfileRank),
            NextRank = source.NextRank == null ? null : context!.Map<RankInfo, RankDto>(source.NextRank),
            Message = source.Message
        });

        configuration.CreateMap<EnrichedExercise, EnrichedExerciseDto>((source, context) => new EnrichedExerciseDto
        {
            ExerciseScoreId = source.ExerciseScoreId.ToString(),
            ExerciseDetails = context!.Map<Exercise, ExerciseResponseDto>(source.ExerciseDetails),
            ScoresDetails = context!.MapList<ExerciseScore, ExerciseScoreResponseDto>(source.ScoresDetails)
        });

        configuration.CreateMap<TrainingByDateDetails, TrainingByDateDetailsDto>((source, context) => new TrainingByDateDetailsDto
        {
            Id = source.Id.ToString(),
            TypePlanDayId = source.TypePlanDayId.ToString(),
            CreatedAt = source.CreatedAt,
            PlanDay = source.PlanDay == null
                ? new PlanDayChooseDto()
                : context!.Map<PlanDay, PlanDayChooseDto>(source.PlanDay),
            Gym = source.Gym,
            Exercises = context!.MapList<EnrichedExercise, EnrichedExerciseDto>(source.Exercises)
        });

        configuration.CreateMap<Training, LastTrainingInfoDto>((source, context) => new LastTrainingInfoDto
        {
            Id = source.Id.ToString(),
            TypePlanDayId = source.TypePlanDayId.ToString(),
            CreatedAt = source.CreatedAt.UtcDateTime,
            PlanDay = source.PlanDay == null
                ? new PlanDayChooseDto()
                : context!.Map<PlanDay, PlanDayChooseDto>(source.PlanDay)
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
