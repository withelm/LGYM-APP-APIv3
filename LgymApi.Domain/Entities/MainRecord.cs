using LgymApi.Domain.Enums;

namespace LgymApi.Domain.Entities;

public sealed class MainRecord : EntityBase
{
    public Guid UserId { get; set; }
    public Guid ExerciseId { get; set; }
    public double Weight { get; set; }
    public WeightUnits Unit { get; set; }
    public DateTimeOffset Date { get; set; }

    public User? User { get; set; }
    public Exercise? Exercise { get; set; }
}
