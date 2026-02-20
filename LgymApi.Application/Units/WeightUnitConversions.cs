using LgymApi.Domain.Enums;

namespace LgymApi.Application.Units;

public static class WeightUnitConversions
{
    private const double PoundsToKilogramsFactor = 0.45359237;

    public static double ToKilograms(double value, WeightUnits unit)
    {
        return unit switch
        {
            WeightUnits.Kilograms => value,
            WeightUnits.Pounds => value * PoundsToKilogramsFactor,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported weight unit.")
        };
    }
}
