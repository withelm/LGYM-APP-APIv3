using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class MainRecord : EntityBase
{
    private double _weightValue;
    private WeightUnits _unit;

    public Guid UserId { get; set; }
    public Guid ExerciseId { get; set; }
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

    public DateTimeOffset Date { get; set; }

    public User? User { get; set; }
    public Exercise? Exercise { get; set; }
}
