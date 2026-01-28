namespace LgymApi.Domain.Entities;

public sealed class ExerciseTranslation : EntityBase
{
    public Guid ExerciseId { get; set; }
    public string Culture { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public Exercise? Exercise { get; set; }
}
