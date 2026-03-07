using LgymApi.Domain.Enums;

namespace LgymApi.Domain.ValueObjects;

public readonly record struct Weight(double Value, WeightUnits Unit)
{
    public static implicit operator Weight(double value) => new(value, WeightUnits.Unknown);

    public static implicit operator double(Weight weight) => weight.Value;

    public Weight ConvertTo(WeightUnits targetUnit, Func<double, WeightUnits, WeightUnits, double> converter)
    {
        if (targetUnit == WeightUnits.Unknown)
        {
            throw new ArgumentException("Target unit cannot be Unknown.", nameof(targetUnit));
        }

        if (Unit == WeightUnits.Unknown)
        {
            throw new InvalidOperationException("Cannot convert weight with Unknown unit.");
        }

        return targetUnit == Unit ? this : new Weight(converter(Value, Unit, targetUnit), targetUnit);
    }
}
