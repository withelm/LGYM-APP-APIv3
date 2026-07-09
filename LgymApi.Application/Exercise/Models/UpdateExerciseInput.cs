using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.Exercise.Models;

public sealed record UpdateExerciseInput(
    Id<LgymApi.Domain.Entities.Exercise> ExerciseId,
    string? Name,
    BodyParts BodyPart,
    string? Description,
    string? Image);

public sealed record UpdateExerciseWithFormulaInput(
    Id<LgymApi.Domain.Entities.Exercise> ExerciseId,
    string? Name,
    BodyParts BodyPart,
    ExerciseEloFormula? EloFormula,
    string? Description,
    string? Image);
