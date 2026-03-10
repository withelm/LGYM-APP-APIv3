using LgymApi.Domain.Enums;

namespace LgymApi.Domain.Tutorials;

public sealed class TutorialDefinition
{
    public TutorialType Type { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<TutorialStep> Steps { get; init; } = new List<TutorialStep>();
}

public static class TutorialDefinitions
{
    public static readonly TutorialDefinition OnboardingDemo = new()
    {
        Type = TutorialType.OnboardingDemo,
        Name = "Onboarding Demo",
        Description = "Complete the onboarding tutorial to get started with the app",
        Steps = new List<TutorialStep>
        {
            TutorialStep.CreateArea,
            TutorialStep.CreateGym,
            TutorialStep.CreatePlan,
            TutorialStep.CreatePlanDay,
            TutorialStep.CreateTraining,
            TutorialStep.LastTreningResult
        }
    };

    public static TutorialDefinition GetByType(TutorialType type)
    {
        return type switch
        {
            TutorialType.OnboardingDemo => OnboardingDemo,
            _ => throw new ArgumentException($"Unknown tutorial type: {type}", nameof(type))
        };
    }

    public static IReadOnlyList<TutorialDefinition> GetAll()
    {
        return new List<TutorialDefinition>
        {
            OnboardingDemo
        };
    }
}
