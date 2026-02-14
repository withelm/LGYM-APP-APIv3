using ExerciseEntity = LgymApi.Domain.Entities.Exercise;

namespace LgymApi.Application.Features.Exercise.Models;

public sealed class ExercisesWithTranslations
{
    public List<ExerciseEntity> Exercises { get; init; } = new();
    public Dictionary<Guid, string> Translations { get; init; } = new();
}
