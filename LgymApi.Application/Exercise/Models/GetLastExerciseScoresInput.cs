namespace LgymApi.Application.Features.Exercise.Models;

public sealed record GetLastExerciseScoresInput(
    Guid RouteUserId,
    Guid CurrentUserId,
    Guid ExerciseId,
    int Series,
    Guid? GymId,
    string ExerciseName);
