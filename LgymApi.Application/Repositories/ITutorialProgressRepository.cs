using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;

namespace LgymApi.Application.Repositories;

public interface ITutorialProgressRepository
{
    Task<UserTutorialProgress?> FindByUserIdAndTypeAsync(Guid userId, TutorialType tutorialType, CancellationToken cancellationToken = default);
    Task<List<UserTutorialProgress>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> HasActiveTutorialsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(UserTutorialProgress progress, CancellationToken cancellationToken = default);
    Task AddStepAsync(Guid progressId, UserTutorialStepProgress step, CancellationToken cancellationToken = default);
    Task UpdateAsync(UserTutorialProgress progress, CancellationToken cancellationToken = default);
}
