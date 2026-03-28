using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.Gym.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Resources;
using LgymApi.Domain.ValueObjects;
using GymEntity = LgymApi.Domain.Entities.Gym;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Gym;

public sealed class GymService : IGymService
{
    private readonly IGymRepository _gymRepository;
    private readonly ITrainingRepository _trainingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public GymService(IGymRepository gymRepository, ITrainingRepository trainingRepository, IUnitOfWork unitOfWork)
    {
        _gymRepository = gymRepository;
        _trainingRepository = trainingRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task AddGymAsync(UserEntity currentUser, Id<LgymApi.Domain.Entities.User> routeUserId, string name, string? address, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || routeUserId.IsEmpty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (currentUser.Id != routeUserId)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        Id<Address>? addressId = null;
        if (!string.IsNullOrWhiteSpace(address) && Id<Address>.TryParse(address, out var parsedAddressId))
        {
            addressId = parsedAddressId;
        }

        var gym = new GymEntity
        {
            Id = Id<LgymApi.Domain.Entities.Gym>.New(),
            UserId = currentUser.Id,
            Name = name,
            AddressId = addressId,
            IsDeleted = false
        };

        await _gymRepository.AddAsync(gym, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteGymAsync(UserEntity currentUser, Id<LgymApi.Domain.Entities.Gym> gymId, CancellationToken cancellationToken = default)
    {
        if (currentUser == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (gymId.IsEmpty)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var gym = await _gymRepository.FindByIdAsync(gymId, cancellationToken);
        if (gym == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (gym.UserId != currentUser.Id)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        gym.IsDeleted = true;
        await _gymRepository.UpdateAsync(gym, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<GymListContext> GetGymsAsync(UserEntity currentUser, Id<LgymApi.Domain.Entities.User> routeUserId, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || routeUserId.IsEmpty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (currentUser.Id != routeUserId)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        var gyms = await _gymRepository.GetByUserIdAsync(currentUser.Id, cancellationToken);
        var gymIds = gyms.Select(g => g.Id).ToList();
        var trainings = await _trainingRepository.GetByGymIdsAsync(gymIds, cancellationToken);
        var lastTrainings = trainings
            .GroupBy(t => t.GymId)
            .Select(g => g.OrderByDescending(t => t.CreatedAt).FirstOrDefault())
            .Where(t => t != null)
            .ToDictionary(t => t!.GymId, t => t!);

        return new GymListContext
        {
            Gyms = gyms,
            LastTrainings = lastTrainings
        };
    }

    public async Task<GymEntity> GetGymAsync(UserEntity currentUser, Id<LgymApi.Domain.Entities.Gym> gymId, CancellationToken cancellationToken = default)
    {
        if (currentUser == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (gymId.IsEmpty)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var gym = await _gymRepository.FindByIdAsync(gymId, cancellationToken);
        if (gym == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (gym.UserId != currentUser.Id)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        return gym;
    }

    public async Task UpdateGymAsync(UserEntity currentUser, Id<LgymApi.Domain.Entities.Gym> gymId, string name, string? address, CancellationToken cancellationToken = default)
    {
        if (currentUser == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (gymId.IsEmpty)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var gym = await _gymRepository.FindByIdAsync(gymId, cancellationToken);
        if (gym == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (gym.UserId != currentUser.Id)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        gym.Name = name;
        if (!string.IsNullOrWhiteSpace(address) && Id<Address>.TryParse(address, out var addressId))
        {
            gym.AddressId = addressId;
        }

        await _gymRepository.UpdateAsync(gym, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
