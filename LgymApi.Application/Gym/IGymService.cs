using LgymApi.Application.Features.Gym.Models;
using UserEntity = LgymApi.Domain.Entities.User;
using GymEntity = LgymApi.Domain.Entities.Gym;

namespace LgymApi.Application.Features.Gym;

public interface IGymService
{
    Task AddGymAsync(UserEntity currentUser, Guid routeUserId, string name, string? address, CancellationToken cancellationToken = default);
    Task DeleteGymAsync(UserEntity currentUser, Guid gymId, CancellationToken cancellationToken = default);
    Task<GymListContext> GetGymsAsync(UserEntity currentUser, Guid routeUserId, CancellationToken cancellationToken = default);
    Task<GymEntity> GetGymAsync(UserEntity currentUser, Guid gymId, CancellationToken cancellationToken = default);
    Task UpdateGymAsync(UserEntity currentUser, Guid gymId, string name, string? address, CancellationToken cancellationToken = default);
}
