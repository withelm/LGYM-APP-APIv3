using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class ExerciseScore : EntityBase
{
    private double _weightValue;
    private WeightUnits _unit;

    public Guid ExerciseId { get; set; }
    public Guid UserId { get; set; }
    public double Reps { get; set; }
    public int Series { get; set; }
    public double WeightValue => _weightValue;
    public Weight Weight
    {
        get => new(_weightValue, _unit);
        set
        {
            _weightValue = value.Value;
            _unit = value.Unit;
        }
    }

    public WeightUnits Unit
    {
        get => _unit;
        set => _unit = value;
    }

    public Guid TrainingId { get; set; }
    public int Order { get; set; }

    public Exercise? Exercise { get; set; }
    public User? User { get; set; }
    public Training? Training { get; set; }
}
