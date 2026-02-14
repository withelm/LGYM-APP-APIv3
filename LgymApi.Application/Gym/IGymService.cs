using LgymApi.Application.Features.Gym.Models;
using UserEntity = LgymApi.Domain.Entities.User;
using GymEntity = LgymApi.Domain.Entities.Gym;

namespace LgymApi.Application.Features.Gym;

public interface IGymService
{
    Task AddGymAsync(UserEntity currentUser, Guid routeUserId, string name, string? address);
    Task DeleteGymAsync(UserEntity currentUser, Guid gymId);
    Task<GymListContext> GetGymsAsync(UserEntity currentUser, Guid routeUserId);
    Task<GymEntity> GetGymAsync(UserEntity currentUser, Guid gymId);
    Task UpdateGymAsync(UserEntity currentUser, Guid gymId, string name, string? address);
}
