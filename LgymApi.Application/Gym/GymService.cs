using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.Gym.Models;
using LgymApi.Application.Repositories;
using LgymApi.Resources;
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

    public async Task AddGymAsync(UserEntity currentUser, Guid routeUserId, string name, string? address, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || routeUserId == Guid.Empty)
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

        Guid? addressId = null;
        if (!string.IsNullOrWhiteSpace(address) && Guid.TryParse(address, out var parsedAddressId))
        {
            addressId = parsedAddressId;
        }

        var gym = new GymEntity
        {
            Id = Guid.NewGuid(),
            UserId = currentUser.Id,
            Name = name,
            AddressId = addressId,
            IsDeleted = false
        };

        await _gymRepository.AddAsync(gym, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteGymAsync(UserEntity currentUser, Guid gymId, CancellationToken cancellationToken = default)
    {
        if (currentUser == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (gymId == Guid.Empty)
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

    public async Task<GymListContext> GetGymsAsync(UserEntity currentUser, Guid routeUserId, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || routeUserId == Guid.Empty)
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

    public async Task<GymEntity> GetGymAsync(UserEntity currentUser, Guid gymId, CancellationToken cancellationToken = default)
    {
        if (currentUser == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (gymId == Guid.Empty)
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

    public async Task UpdateGymAsync(UserEntity currentUser, Guid gymId, string name, string? address, CancellationToken cancellationToken = default)
    {
        if (currentUser == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (gymId == Guid.Empty)
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
        if (!string.IsNullOrWhiteSpace(address) && Guid.TryParse(address, out var addressId))
        {
            gym.AddressId = addressId;
        }

        await _gymRepository.UpdateAsync(gym, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
