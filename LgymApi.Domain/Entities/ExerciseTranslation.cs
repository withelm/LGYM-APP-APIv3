using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class ExerciseTranslation : EntityBase<ExerciseTranslation>
{
    public Id<Exercise> ExerciseId { get; set; }
    public string Culture { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public Exercise? Exercise { get; set; }
}
