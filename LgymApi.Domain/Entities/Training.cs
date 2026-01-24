namespace LgymApi.Domain.Entities;

public sealed class Training : EntityBase
{
    public Guid UserId { get; set; }
    public Guid TypePlanDayId { get; set; }
    public Guid GymId { get; set; }

    public User? User { get; set; }
    public PlanDay? PlanDay { get; set; }
    public Gym? Gym { get; set; }
    public ICollection<TrainingExerciseScore> Exercises { get; set; } = new List<TrainingExerciseScore>();
}
