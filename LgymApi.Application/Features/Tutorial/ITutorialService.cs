using LgymApi.Application.Features.Tutorial.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Tutorial;

public interface ITutorialService
{
    Task InitializeOnboardingTutorialAsync(Id<UserEntity> userId, CancellationToken cancellationToken = default);
    Task<List<TutorialProgressResult>> GetActiveTutorialsAsync(Id<UserEntity> userId, CancellationToken cancellationToken = default);
    Task<TutorialProgressResult?> GetTutorialProgressAsync(Id<UserEntity> userId, TutorialType tutorialType, CancellationToken cancellationToken = default);
    Task CompleteStepAsync(Id<UserEntity> userId, TutorialType tutorialType, TutorialStep step, CancellationToken cancellationToken = default);
    Task CompleteTutorialAsync(Id<UserEntity> userId, TutorialType tutorialType, CancellationToken cancellationToken = default);
    Task<bool> HasActiveTutorialsAsync(Id<UserEntity> userId, CancellationToken cancellationToken = default);
}
