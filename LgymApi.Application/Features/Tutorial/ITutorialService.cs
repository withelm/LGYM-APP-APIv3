using LgymApi.Application.Features.Tutorial.Models;
using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.Tutorial;

public interface ITutorialService
{
    Task InitializeOnboardingTutorialAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<TutorialProgressResult>> GetActiveTutorialsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<TutorialProgressResult?> GetTutorialProgressAsync(Guid userId, TutorialType tutorialType, CancellationToken cancellationToken = default);
    Task CompleteStepAsync(Guid userId, TutorialType tutorialType, TutorialStep step, CancellationToken cancellationToken = default);
    Task CompleteTutorialAsync(Guid userId, TutorialType tutorialType, CancellationToken cancellationToken = default);
    Task<bool> HasActiveTutorialsAsync(Guid userId, CancellationToken cancellationToken = default);
}
