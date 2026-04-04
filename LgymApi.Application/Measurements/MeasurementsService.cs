using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.Measurements.Models;
using LgymApi.Application.Repositories;
using LgymApi.Application.Units;
using LgymApi.Domain.ValueObjects;
using LgymApi.Domain.Enums;
using LgymApi.Resources;
using MeasurementEntity = LgymApi.Domain.Entities.Measurement;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Measurements;

public sealed class MeasurementsService : IMeasurementsService
{
    private readonly IMeasurementRepository _measurementRepository;
    private readonly IUnitConverter<HeightUnits> _heightUnitConverter;
    private readonly IUnitOfWork _unitOfWork;

    public MeasurementsService(
        IMeasurementRepository measurementRepository,
        IUnitConverter<HeightUnits> heightUnitConverter,
        IUnitOfWork unitOfWork)
    {
        _measurementRepository = measurementRepository;
        _heightUnitConverter = heightUnitConverter;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit, AppError>> AddMeasurementAsync(UserEntity currentUser, BodyParts bodyPart, HeightUnits unit, double value, CancellationToken cancellationToken = default)
    {
        if (currentUser == null)
        {
            return Result<Unit, AppError>.Failure(new MeasurementNotFoundError(Messages.DidntFind));
        }

        if (bodyPart == BodyParts.Unknown || unit == HeightUnits.Unknown)
        {
            return Result<Unit, AppError>.Failure(new InvalidMeasurementError(Messages.FieldRequired));
        }

        var measurement = new MeasurementEntity
        {
            Id = Id<MeasurementEntity>.New(),
            UserId = currentUser.Id,
            BodyPart = bodyPart,
            Unit = unit.ToString(),
            Value = value
        };

        await _measurementRepository.AddAsync(measurement, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<MeasurementEntity, AppError>> GetMeasurementDetailAsync(UserEntity currentUser, Id<MeasurementEntity> measurementId, CancellationToken cancellationToken = default)
    {
        if (currentUser == null)
        {
            return Result<MeasurementEntity, AppError>.Failure(new MeasurementNotFoundError(Messages.DidntFind));
        }

        if (measurementId.IsEmpty)
        {
            return Result<MeasurementEntity, AppError>.Failure(new MeasurementNotFoundError(Messages.DidntFind));
        }

        var measurement = await _measurementRepository.FindByIdAsync(measurementId, cancellationToken);
        if (measurement == null)
        {
            return Result<MeasurementEntity, AppError>.Failure(new MeasurementNotFoundError(Messages.DidntFind));
        }

        if (measurement.UserId != currentUser.Id)
        {
            return Result<MeasurementEntity, AppError>.Failure(new MeasurementForbiddenError(Messages.Forbidden));
        }

        return Result<MeasurementEntity, AppError>.Success(measurement);
    }

    public Task<Result<List<MeasurementEntity>, AppError>> GetMeasurementsListAsync(UserEntity currentUser, Id<UserEntity> routeUserId, BodyParts? bodyPart, HeightUnits? unit, CancellationToken cancellationToken = default)
    {
        return GetMeasurementsInternalAsync(currentUser, routeUserId, bodyPart, unit, orderAscending: false, cancellationToken);
    }

    public Task<Result<List<MeasurementEntity>, AppError>> GetMeasurementsHistoryAsync(UserEntity currentUser, Id<UserEntity> routeUserId, BodyParts? bodyPart, HeightUnits? unit, CancellationToken cancellationToken = default)
    {
        return GetMeasurementsInternalAsync(currentUser, routeUserId, bodyPart, unit, orderAscending: true, cancellationToken);
    }

    public async Task<Result<MeasurementTrendResult, AppError>> GetMeasurementsTrendAsync(
        UserEntity currentUser,
        Id<UserEntity> routeUserId,
        BodyParts bodyPart,
        HeightUnits unit,
        CancellationToken cancellationToken = default)
    {
        var accessValidation = ValidateAccess(currentUser, routeUserId);
        if (accessValidation.IsFailure)
        {
            return Result<MeasurementTrendResult, AppError>.Failure(accessValidation.Error);
        }

        if (!System.Enum.IsDefined(bodyPart) || bodyPart == BodyParts.Unknown)
        {
            return Result<MeasurementTrendResult, AppError>.Failure(new InvalidMeasurementError(Messages.BodyPartRequired));
        }

        if (!System.Enum.IsDefined(unit) || unit == HeightUnits.Unknown)
        {
            return Result<MeasurementTrendResult, AppError>.Failure(new InvalidMeasurementError(Messages.UnitRequired));
        }

        var measurements = await _measurementRepository.GetByUserAsync(currentUser.Id, bodyPart, cancellationToken);
        if (measurements.Count < 1)
        {
            return Result<MeasurementTrendResult, AppError>.Failure(new MeasurementNotFoundError(Messages.DidntFind));
        }

        var ordered = measurements.OrderBy(m => m.CreatedAt).ToList();
        var convertedValues = ordered
            .Select(m => ConvertValue(m.Value, ParseHeightUnit(m.Unit), unit))
            .ToList();

        var startValue = convertedValues[0];
        var currentValue = convertedValues[^1];
        var change = currentValue - startValue;
        var roundedChange = Math.Round(change, 2);
        const double percentageEpsilon = 0.0001d;
        var changePercentage = Math.Abs(startValue) <= percentageEpsilon
            ? 0d
            : (change / startValue) * 100d;

        return Result<MeasurementTrendResult, AppError>.Success(new MeasurementTrendResult
        {
            BodyPart = bodyPart,
            Unit = unit,
            StartValue = Math.Round(startValue, 2),
            CurrentValue = Math.Round(currentValue, 2),
            Change = roundedChange,
            ChangePercentage = Math.Round(changePercentage, 2),
            Direction = ResolveDirection(roundedChange),
            Points = ordered.Count
        });
    }

    private async Task<Result<List<MeasurementEntity>, AppError>> GetMeasurementsInternalAsync(
        UserEntity currentUser,
        Id<UserEntity> routeUserId,
        BodyParts? bodyPart,
        HeightUnits? unit,
        bool orderAscending,
        CancellationToken cancellationToken)
    {
        var accessValidation = ValidateAccess(currentUser, routeUserId);
        if (accessValidation.IsFailure)
        {
            return Result<List<MeasurementEntity>, AppError>.Failure(accessValidation.Error);
        }

        if (bodyPart.HasValue && (!System.Enum.IsDefined(bodyPart.Value) || bodyPart.Value == BodyParts.Unknown))
        {
            return Result<List<MeasurementEntity>, AppError>.Failure(new InvalidMeasurementError(Messages.BodyPartRequired));
        }

        if (unit.HasValue && (!System.Enum.IsDefined(unit.Value) || unit.Value == HeightUnits.Unknown))
        {
            return Result<List<MeasurementEntity>, AppError>.Failure(new InvalidMeasurementError(Messages.UnitRequired));
        }

        var measurements = await _measurementRepository.GetByUserAsync(currentUser.Id, bodyPart, cancellationToken);
        if (measurements.Count < 1)
        {
            return Result<List<MeasurementEntity>, AppError>.Failure(new MeasurementNotFoundError(Messages.DidntFind));
        }

        var ordered = orderAscending
            ? measurements.OrderBy(m => m.CreatedAt)
            : measurements.OrderByDescending(m => m.CreatedAt);

        var result = new List<MeasurementEntity>();
        foreach (var m in ordered)
        {
            var parsedUnit = ParseHeightUnit(m.Unit);
            if (parsedUnit == HeightUnits.Unknown)
            {
                return Result<List<MeasurementEntity>, AppError>.Failure(new InvalidMeasurementError(Messages.UnitRequired));
            }

            var targetUnit = unit ?? parsedUnit;
            var convertedValue = ConvertValue(m.Value, parsedUnit, targetUnit);

            result.Add(new MeasurementEntity
            {
                Id = m.Id,
                UserId = m.UserId,
                BodyPart = m.BodyPart,
                Unit = targetUnit.ToString(),
                Value = Math.Round(convertedValue, 2),
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt
            });
        }

        return Result<List<MeasurementEntity>, AppError>.Success(result);
    }

    private static Result<Unit, AppError> ValidateAccess(UserEntity currentUser, Id<UserEntity> routeUserId)
    {
        if (currentUser == null || routeUserId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new MeasurementNotFoundError(Messages.DidntFind));
        }

        if (currentUser.Id != routeUserId)
        {
            return Result<Unit, AppError>.Failure(new MeasurementForbiddenError(Messages.Forbidden));
        }

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private static string ResolveDirection(double change)
    {
        const double epsilon = 0.0001d;
        if (change > epsilon)
        {
            return "up";
        }

        if (change < -epsilon)
        {
            return "down";
        }

        return "flat";
    }

    private static HeightUnits ParseHeightUnit(string? unit)
    {
        if (!string.IsNullOrWhiteSpace(unit) && System.Enum.TryParse(unit, true, out HeightUnits parsed) && parsed != HeightUnits.Unknown)
        {
            return parsed;
        }

        // This is internal validation logic that should not happen with valid stored data
        // In a Result pattern, we rely on earlier validation - this should never be reached
        // but keeping defensive coding with a fallback to avoid runtime crashes
        return HeightUnits.Unknown;
    }

    private double ConvertValue(double value, HeightUnits fromUnit, HeightUnits toUnit)
    {
        return _heightUnitConverter.Convert(value, fromUnit, toUnit);
    }
}
