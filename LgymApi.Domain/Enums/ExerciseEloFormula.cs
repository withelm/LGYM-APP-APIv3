using LgymApi.Resources;

namespace LgymApi.Domain.Enums;

public enum ExerciseEloFormula
{
    [EnumTranslation(EnumTranslationKeys.ExerciseEloFormula.Standard)]
    Standard = 0,
    [EnumTranslation(EnumTranslationKeys.ExerciseEloFormula.StrengthWeighted)]
    StrengthWeighted = 1,
    [EnumTranslation(EnumTranslationKeys.ExerciseEloFormula.VolumeWeighted)]
    VolumeWeighted = 2,
    [EnumTranslation(EnumTranslationKeys.ExerciseEloFormula.PullupWeighted)]
    PullupWeighted = 3
}
