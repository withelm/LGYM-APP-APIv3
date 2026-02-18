using LgymApi.Application.Exceptions;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Enums;
using LgymApi.Resources;
using MeasurementEntity = LgymApi.Domain.Entities.Measurement;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Measurements;

public sealed class MeasurementsService : IMeasurementsService
{
    private readonly IMeasurementRepository _measurementRepository;

    public MeasurementsService(IMeasurementRepository measurementRepository)
    {
        _measurementRepository = measurementRepository;
    }

    public async Task AddMeasurementAsync(UserEntity currentUser, BodyParts bodyPart, HeightUnits unit, double value)
    {
        if (currentUser == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var measurement = new MeasurementEntity
        {
            Id = Guid.NewGuid(),
            UserId = currentUser.Id,
            BodyPart = bodyPart,
            Unit = unit.ToString(),
            Value = value
        };

        await _measurementRepository.AddAsync(measurement);
    }

    public async Task<MeasurementEntity> GetMeasurementDetailAsync(UserEntity currentUser, Guid measurementId)
    {
        if (currentUser == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (measurementId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var measurement = await _measurementRepository.FindByIdAsync(measurementId);
        if (measurement == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (measurement.UserId != currentUser.Id)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        return measurement;
    }

    public async Task<List<MeasurementEntity>> GetMeasurementsHistoryAsync(UserEntity currentUser, Guid routeUserId, BodyParts? bodyPart)
    {
        if (currentUser == null || routeUserId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (currentUser.Id != routeUserId)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        var measurements = await _measurementRepository.GetByUserAsync(currentUser.Id, bodyPart);
        if (measurements.Count < 1)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return measurements.OrderBy(m => m.CreatedAt).ToList();
    }
}
