using LgymApi.Resources;

namespace LgymApi.Domain.Enums;

public enum Platforms
{
    [EnumTranslation(EnumTranslationKeys.Platforms.Unknown)]
    [Hidden]
    Unknown = 0,
    [EnumTranslation(EnumTranslationKeys.Platforms.Android)]
    Android = 1,
    [EnumTranslation(EnumTranslationKeys.Platforms.Ios)]
    Ios = 2
}
