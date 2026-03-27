using LgymApi.Domain.Enums;
using LgymApi.Application.Features.Measurements.Models;
using LgymApi.Domain.ValueObjects;
using MeasurementEntity = LgymApi.Domain.Entities.Measurement;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Measurements;

public interface IMeasurementsService
{
    Task AddMeasurementAsync(UserEntity currentUser, BodyParts bodyPart, HeightUnits unit, double value, CancellationToken cancellationToken = default);
    Task<MeasurementEntity> GetMeasurementDetailAsync(UserEntity currentUser, Id<MeasurementEntity> measurementId, CancellationToken cancellationToken = default);
    Task<List<MeasurementEntity>> GetMeasurementsListAsync(UserEntity currentUser, Id<UserEntity> routeUserId, BodyParts? bodyPart, HeightUnits? unit, CancellationToken cancellationToken = default);
    Task<List<MeasurementEntity>> GetMeasurementsHistoryAsync(UserEntity currentUser, Id<UserEntity> routeUserId, BodyParts? bodyPart, HeightUnits? unit, CancellationToken cancellationToken = default);
    Task<MeasurementTrendResult> GetMeasurementsTrendAsync(UserEntity currentUser, Id<UserEntity> routeUserId, BodyParts bodyPart, HeightUnits unit, CancellationToken cancellationToken = default);
}
