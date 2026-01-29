using LgymApi.Domain.Enums;

namespace LgymApi.Domain.Entities;

public sealed class Exercise : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public BodyParts BodyPart { get; set; }
    public string? Description { get; set; }
    public string? Image { get; set; }
    public Guid? UserId { get; set; }
    public bool IsDeleted { get; set; }

    public User? User { get; set; }
    public ICollection<ExerciseScore> ExerciseScores { get; set; } = new List<ExerciseScore>();
    public ICollection<ExerciseTranslation> Translations { get; set; } = new List<ExerciseTranslation>();
}
