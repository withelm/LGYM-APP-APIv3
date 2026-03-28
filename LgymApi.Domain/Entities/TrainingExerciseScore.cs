namespace LgymApi.Domain.Entities;

public sealed class TrainingExerciseScore : EntityBase<TrainingExerciseScore>
{
    public ValueObjects.Id<Training> TrainingId { get; set; }
    public ValueObjects.Id<ExerciseScore> ExerciseScoreId { get; set; }
    public Training? Training { get; set; }
    public ExerciseScore? ExerciseScore { get; set; }
    public int Order { get; set; }
}
