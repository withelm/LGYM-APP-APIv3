using LgymApi.Resources;

namespace LgymApi.Domain.Enums;

public enum WeightUnits
{
    [EnumTranslation(EnumTranslationKeys.WeightUnits.Unknown)]
    [Hidden]
    Unknown = 0,
    [EnumTranslation(EnumTranslationKeys.WeightUnits.Kilograms)]
    Kilograms = 1,
    [EnumTranslation(EnumTranslationKeys.WeightUnits.Pounds)]
    Pounds = 2
}

public enum HeightUnits
{
    [EnumTranslation(EnumTranslationKeys.HeightUnits.Unknown)]
    [Hidden]
    Unknown = 0,
    [EnumTranslation(EnumTranslationKeys.HeightUnits.Meters)]
    Meters = 1,
    [EnumTranslation(EnumTranslationKeys.HeightUnits.Centimeters)]
    Centimeters = 2,
    [EnumTranslation(EnumTranslationKeys.HeightUnits.Millimeters)]
    Millimeters = 3
}

public enum MeasurementUnits
{
    [EnumTranslation(EnumTranslationKeys.MeasurementUnits.Unknown)]
    [Hidden]
    Unknown = 0,
    [EnumTranslation(EnumTranslationKeys.MeasurementUnits.Kilograms)]
    Kilograms = 1,
    [EnumTranslation(EnumTranslationKeys.MeasurementUnits.Pounds)]
    Pounds = 2,
    [EnumTranslation(EnumTranslationKeys.MeasurementUnits.Meters)]
    Meters = 3,
    [EnumTranslation(EnumTranslationKeys.MeasurementUnits.Centimeters)]
    Centimeters = 4,
    [EnumTranslation(EnumTranslationKeys.MeasurementUnits.Millimeters)]
    Millimeters = 5,
    [EnumTranslation(EnumTranslationKeys.MeasurementUnits.Percent)]
    Percent = 6,
    [EnumTranslation(EnumTranslationKeys.MeasurementUnits.Bmi)]
    Bmi = 7
}
