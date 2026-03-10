using LgymApi.Resources;

namespace LgymApi.Domain.Enums;

public enum TutorialType
{
    [EnumTranslation(EnumTranslationKeys.TutorialType.Unknown)]
    [Hidden]
    Unknown = 0,
    [EnumTranslation(EnumTranslationKeys.TutorialType.OnboardingDemo)]
    OnboardingDemo = 1
}
