using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.Exercise.Models;

public sealed record GetLastExerciseScoresInput(
    Id<LgymApi.Domain.Entities.User> RouteUserId,
    Id<LgymApi.Domain.Entities.User> CurrentUserId,
    Id<LgymApi.Domain.Entities.Exercise> ExerciseId,
    int Series,
    Id<LgymApi.Domain.Entities.Gym>? GymId,
    string ExerciseName);
