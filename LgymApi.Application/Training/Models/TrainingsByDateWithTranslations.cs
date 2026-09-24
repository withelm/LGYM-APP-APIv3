using ExerciseEntity = LgymApi.Domain.Entities.Exercise;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.Training.Models;

public sealed class TrainingsByDateWithTranslations
{
    public List<TrainingByDateDetails> Trainings { get; init; } = new();
    public IReadOnlyDictionary<Id<ExerciseEntity>, string> Translations { get; init; } = new Dictionary<Id<ExerciseEntity>, string>();
}
