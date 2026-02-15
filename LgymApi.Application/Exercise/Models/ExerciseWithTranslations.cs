using ExerciseEntity = LgymApi.Domain.Entities.Exercise;

namespace LgymApi.Application.Features.Exercise.Models;

public sealed class ExerciseWithTranslations
{
    public ExerciseEntity Exercise { get; init; } = null!;
    public Dictionary<Guid, string> Translations { get; init; } = new();
}
