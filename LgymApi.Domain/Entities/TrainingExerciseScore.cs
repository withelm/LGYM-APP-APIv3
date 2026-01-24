namespace LgymApi.Domain.Entities;

public sealed class TrainingExerciseScore : EntityBase
{
    public Guid TrainingId { get; set; }
    public Guid ExerciseScoreId { get; set; }

    public Training? Training { get; set; }
    public ExerciseScore? ExerciseScore { get; set; }
}
