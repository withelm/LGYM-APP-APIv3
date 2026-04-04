using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Domain.Enums;
using LgymApi.Application.Features.Measurements.Models;
using LgymApi.Domain.ValueObjects;
using MeasurementEntity = LgymApi.Domain.Entities.Measurement;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Measurements;

public interface IMeasurementsService
{
    Task<Result<Unit, AppError>> AddMeasurementAsync(UserEntity currentUser, BodyParts bodyPart, HeightUnits unit, double value, CancellationToken cancellationToken = default);
    Task<Result<MeasurementEntity, AppError>> GetMeasurementDetailAsync(UserEntity currentUser, Id<MeasurementEntity> measurementId, CancellationToken cancellationToken = default);
    Task<Result<List<MeasurementEntity>, AppError>> GetMeasurementsListAsync(UserEntity currentUser, Id<UserEntity> routeUserId, BodyParts? bodyPart, HeightUnits? unit, CancellationToken cancellationToken = default);
    Task<Result<List<MeasurementEntity>, AppError>> GetMeasurementsHistoryAsync(UserEntity currentUser, Id<UserEntity> routeUserId, BodyParts? bodyPart, HeightUnits? unit, CancellationToken cancellationToken = default);
    Task<Result<MeasurementTrendResult, AppError>> GetMeasurementsTrendAsync(UserEntity currentUser, Id<UserEntity> routeUserId, BodyParts bodyPart, HeightUnits unit, CancellationToken cancellationToken = default);
}
