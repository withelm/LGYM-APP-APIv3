using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.TrainingPlanning.Contracts.PlanDay;

public interface IPlanDayRelationshipAccessPort
{
    Task<bool> HasActiveRelationshipAsync(
        Id<UserEntity> trainerId,
        Id<UserEntity> traineeId,
        CancellationToken cancellationToken = default);
}
