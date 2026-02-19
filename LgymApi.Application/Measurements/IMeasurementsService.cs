using LgymApi.Domain.Enums;
using MeasurementEntity = LgymApi.Domain.Entities.Measurement;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Measurements;

public interface IMeasurementsService
{
    Task AddMeasurementAsync(UserEntity currentUser, BodyParts bodyPart, HeightUnits unit, double value);
    Task<MeasurementEntity> GetMeasurementDetailAsync(UserEntity currentUser, Guid measurementId);
    Task<List<MeasurementEntity>> GetMeasurementsHistoryAsync(UserEntity currentUser, Guid routeUserId, BodyParts? bodyPart);
}
