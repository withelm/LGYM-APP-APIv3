namespace LgymApi.Domain.Entities;

public sealed class Training : EntityBase<Training>
{
    public ValueObjects.Id<User> UserId { get; set; }
    public ValueObjects.Id<PlanDay> TypePlanDayId { get; set; }
    public ValueObjects.Id<Gym> GymId { get; set; }

    public User? User { get; set; }
    public PlanDay? PlanDay { get; set; }
    public Gym? Gym { get; set; }
    public ICollection<TrainingExerciseScore> Exercises { get; set; } = new List<TrainingExerciseScore>();
}
