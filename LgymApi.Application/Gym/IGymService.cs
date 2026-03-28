using LgymApi.Application.Features.Gym.Models;
using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;
using GymEntity = LgymApi.Domain.Entities.Gym;

namespace LgymApi.Application.Features.Gym;

public interface IGymService
{
    Task AddGymAsync(UserEntity currentUser, Id<LgymApi.Domain.Entities.User> routeUserId, string name, string? address, CancellationToken cancellationToken = default);
    Task DeleteGymAsync(UserEntity currentUser, Id<LgymApi.Domain.Entities.Gym> gymId, CancellationToken cancellationToken = default);
    Task<GymListContext> GetGymsAsync(UserEntity currentUser, Id<LgymApi.Domain.Entities.User> routeUserId, CancellationToken cancellationToken = default);
    Task<GymEntity> GetGymAsync(UserEntity currentUser, Id<LgymApi.Domain.Entities.Gym> gymId, CancellationToken cancellationToken = default);
    Task UpdateGymAsync(UserEntity currentUser, Id<LgymApi.Domain.Entities.Gym> gymId, string name, string? address, CancellationToken cancellationToken = default);
}
