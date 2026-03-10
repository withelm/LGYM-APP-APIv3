using System.Text.Json.Serialization;
using LgymApi.Api.Interfaces;
using LgymApi.Domain.Enums;

namespace LgymApi.Api.Features.Tutorial.Contracts;

public sealed class CompleteStepRequest : IDto
{
    [JsonPropertyName("tutorialType")]
    public TutorialType TutorialType { get; set; }

    [JsonPropertyName("step")]
    public TutorialStep Step { get; set; }
}

public sealed class CompleteTutorialRequest : IDto
{
    [JsonPropertyName("tutorialType")]
    public TutorialType TutorialType { get; set; }
}

public sealed class TutorialProgressDto : IResultDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("tutorialType")]
    public TutorialType TutorialType { get; set; }

    [JsonPropertyName("tutorialName")]
    public string TutorialName { get; set; } = string.Empty;

    [JsonPropertyName("tutorialDescription")]
    public string TutorialDescription { get; set; } = string.Empty;

    [JsonPropertyName("isCompleted")]
    public bool IsCompleted { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("completedSteps")]
    public List<TutorialStep> CompletedSteps { get; set; } = new();

    [JsonPropertyName("remainingSteps")]
    public List<TutorialStep> RemainingSteps { get; set; } = new();

    [JsonPropertyName("totalSteps")]
    public int TotalSteps { get; set; }

    [JsonPropertyName("completedStepsCount")]
    public int CompletedStepsCount { get; set; }
}
