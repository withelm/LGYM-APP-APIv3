using LgymApi.Domain.Enums;

namespace LgymApi.Domain.Entities;

public sealed class ExerciseScore : EntityBase
{
    public Guid ExerciseId { get; set; }
    public Guid UserId { get; set; }
    public int Reps { get; set; }
    public int Series { get; set; }
    public double Weight { get; set; }
    public WeightUnits Unit { get; set; }
    public Guid TrainingId { get; set; }

    public Exercise? Exercise { get; set; }
    public User? User { get; set; }
    public Training? Training { get; set; }
}
