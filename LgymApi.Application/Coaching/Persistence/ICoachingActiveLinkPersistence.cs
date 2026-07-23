using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Coaching.Persistence;

public interface ICoachingActiveLinkPersistence
{
    Task AddAsync(CoachingActiveLinkWriteModel link, CancellationToken cancellationToken = default);
    Task RemoveAsync(Id<TrainerTraineeLink> linkId, CancellationToken cancellationToken = default);
    Task<bool> HasForTraineeAsync(Id<User> traineeId, CancellationToken cancellationToken = default);
    Task<CoachingActiveLinkFact?> FindByTrainerAndTraineeAsync(Id<User> trainerId, Id<User> traineeId, CancellationToken cancellationToken = default);
    Task<CoachingActiveLinkFact?> FindByTraineeAsync(Id<User> traineeId, CancellationToken cancellationToken = default);
}
