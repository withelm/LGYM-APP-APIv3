using LgymApi.Resources;

namespace LgymApi.Domain.Enums;

public enum TutorialStep
{
    [EnumTranslation(EnumTranslationKeys.TutorialStep.Unknown)]
    [Hidden]
    Unknown = 0,
    [EnumTranslation(EnumTranslationKeys.TutorialStep.CreateArea)]
    CreateArea = 1,
    [EnumTranslation(EnumTranslationKeys.TutorialStep.CreateGym)]
    CreateGym = 2,
    [EnumTranslation(EnumTranslationKeys.TutorialStep.CreatePlan)]
    CreatePlan = 3,
    [EnumTranslation(EnumTranslationKeys.TutorialStep.CreatePlanDay)]
    CreatePlanDay = 4,
    [EnumTranslation(EnumTranslationKeys.TutorialStep.CreateTraining)]
    CreateTraining = 5,
    [EnumTranslation(EnumTranslationKeys.TutorialStep.LastTreningResult)]
    LastTreningResult = 6
}
