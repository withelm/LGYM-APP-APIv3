using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.WorkoutProgress.Contracts.Measurements;

public interface IMeasurementsRelationshipAccessPort
{
    Task<bool> HasActiveRelationshipAsync(
        Id<UserEntity> trainerId,
        Id<UserEntity> traineeId,
        CancellationToken cancellationToken = default);
}
