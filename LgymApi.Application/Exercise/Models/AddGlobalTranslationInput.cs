namespace LgymApi.Application.Features.Exercise.Models;

public sealed record AddGlobalTranslationInput(
    Guid RouteUserId,
    string ExerciseId,
    string? Culture,
    string? Name);
