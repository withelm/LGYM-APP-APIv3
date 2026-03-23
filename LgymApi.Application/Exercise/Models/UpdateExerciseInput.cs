using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.Exercise.Models;

public sealed record UpdateExerciseInput(
    string ExerciseId,
    string? Name,
    BodyParts BodyPart,
    string? Description,
    string? Image);
