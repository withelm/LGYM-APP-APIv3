using LgymApi.Domain.ValueObjects;
using ExerciseEntity = LgymApi.Domain.Entities.Exercise;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Coaching.Progress.ExerciseScoresChart;

public sealed record GetExerciseScoresChartQuery(
    Id<UserEntity> TrainerId,
    Id<UserEntity> TraineeId,
    Id<ExerciseEntity> ExerciseId);
