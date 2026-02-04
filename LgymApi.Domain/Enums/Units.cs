using LgymApi.Resources;

namespace LgymApi.Domain.Enums;

public enum WeightUnits
{
    [EnumTranslation(EnumTranslationKeys.WeightUnits.Unknown)]
    Unknown = 0,
    [EnumTranslation(EnumTranslationKeys.WeightUnits.Kilograms)]
    Kilograms = 1,
    [EnumTranslation(EnumTranslationKeys.WeightUnits.Pounds)]
    Pounds = 2
}

public enum HeightUnits
{
    [EnumTranslation(EnumTranslationKeys.HeightUnits.Unknown)]
    Unknown = 0,
    [EnumTranslation(EnumTranslationKeys.HeightUnits.Meters)]
    Meters = 1,
    [EnumTranslation(EnumTranslationKeys.HeightUnits.Centimeters)]
    Centimeters = 2,
    [EnumTranslation(EnumTranslationKeys.HeightUnits.Millimeters)]
    Millimeters = 3
}
