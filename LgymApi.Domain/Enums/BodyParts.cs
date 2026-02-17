using LgymApi.Resources;

namespace LgymApi.Domain.Enums;

public enum BodyParts
{
    [EnumTranslation(EnumTranslationKeys.BodyParts.Unknown)]
    [Hidden]
    Unknown = 0,
    [EnumTranslation(EnumTranslationKeys.BodyParts.Chest)]
    Chest = 1,
    [EnumTranslation(EnumTranslationKeys.BodyParts.Back)]
    Back = 2,
    [EnumTranslation(EnumTranslationKeys.BodyParts.Shoulders)]
    Shoulders = 3,
    [EnumTranslation(EnumTranslationKeys.BodyParts.Biceps)]
    Biceps = 4,
    [EnumTranslation(EnumTranslationKeys.BodyParts.Triceps)]
    Triceps = 5,
    [EnumTranslation(EnumTranslationKeys.BodyParts.Forearms)]
    Forearms = 6,
    [EnumTranslation(EnumTranslationKeys.BodyParts.Abs)]
    Abs = 7,
    [EnumTranslation(EnumTranslationKeys.BodyParts.Quads)]
    Quads = 8,
    [EnumTranslation(EnumTranslationKeys.BodyParts.Hamstrings)]
    Hamstrings = 9,
    [EnumTranslation(EnumTranslationKeys.BodyParts.Calves)]
    Calves = 10,
    [EnumTranslation(EnumTranslationKeys.BodyParts.Glutes)]
    Glutes = 11
}
