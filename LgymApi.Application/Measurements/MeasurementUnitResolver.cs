using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.Measurements;

public static class MeasurementUnitResolver
{
    public static bool IsSpecialUnitMeasurement(BodyParts bodyPart)
        => bodyPart is BodyParts.BodyFat or BodyParts.Bmi;

    public static bool IsWeightMeasurement(BodyParts bodyPart)
        => bodyPart == BodyParts.BodyWeight;

    public static bool IsLengthMeasurement(BodyParts bodyPart)
        => bodyPart != BodyParts.Unknown && !IsWeightMeasurement(bodyPart) && !IsSpecialUnitMeasurement(bodyPart);

    public static bool IsUnitAllowedForBodyPart(BodyParts bodyPart, MeasurementUnits unit)
    {
        if (bodyPart == BodyParts.Unknown || unit == MeasurementUnits.Unknown)
        {
            return false;
        }

        return bodyPart switch
        {
            BodyParts.BodyWeight => unit is MeasurementUnits.Kilograms or MeasurementUnits.Pounds,
            BodyParts.BodyFat => unit == MeasurementUnits.Percent,
            BodyParts.Bmi => unit == MeasurementUnits.Bmi,
            _ => unit is MeasurementUnits.Meters or MeasurementUnits.Centimeters or MeasurementUnits.Millimeters
        };
    }

    public static MeasurementUnits GetDefaultUnit(BodyParts bodyPart)
        => bodyPart switch
        {
            BodyParts.BodyWeight => MeasurementUnits.Kilograms,
            BodyParts.BodyFat => MeasurementUnits.Percent,
            BodyParts.Bmi => MeasurementUnits.Bmi,
            _ => MeasurementUnits.Centimeters
        };

    public static bool TryParseStoredUnit(string? rawUnit, out MeasurementUnits unit)
    {
        if (!string.IsNullOrWhiteSpace(rawUnit) && System.Enum.TryParse(rawUnit, true, out MeasurementUnits parsed) && parsed != MeasurementUnits.Unknown)
        {
            unit = parsed;
            return true;
        }

        unit = MeasurementUnits.Unknown;
        return false;
    }

    public static bool TryGetHeightUnit(MeasurementUnits unit, out HeightUnits heightUnit)
    {
        heightUnit = unit switch
        {
            MeasurementUnits.Meters => HeightUnits.Meters,
            MeasurementUnits.Centimeters => HeightUnits.Centimeters,
            MeasurementUnits.Millimeters => HeightUnits.Millimeters,
            _ => HeightUnits.Unknown
        };

        return heightUnit != HeightUnits.Unknown;
    }

    public static bool TryGetWeightUnit(MeasurementUnits unit, out WeightUnits weightUnit)
    {
        weightUnit = unit switch
        {
            MeasurementUnits.Kilograms => WeightUnits.Kilograms,
            MeasurementUnits.Pounds => WeightUnits.Pounds,
            _ => WeightUnits.Unknown
        };

        return weightUnit != WeightUnits.Unknown;
    }
}
