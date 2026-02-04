using LgymApi.Resources;

namespace LgymApi.Domain.Enums;

public enum Platforms
{
    [EnumTranslation(EnumTranslationKeys.Platforms.Android)]
    Android = 1,
    [EnumTranslation(EnumTranslationKeys.Platforms.Ios)]
    Ios = 2
}
