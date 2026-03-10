using LgymApi.Resources;

namespace LgymApi.Domain.Enums;

public enum EloStrategy
{
    [EnumTranslation(EnumTranslationKeys.EloStrategy.Standard)]
    Standard = 0,

    [EnumTranslation(EnumTranslationKeys.EloStrategy.Assistance)]
    Assistance = 1
}
