using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.Measurements.Models;
using LgymApi.Application.Repositories;
using LgymApi.Application.Units;
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

    public async Task AddMeasurementAsync(UserEntity currentUser, BodyParts bodyPart, HeightUnits unit, double value)
    {
        if (currentUser == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (bodyPart == BodyParts.Unknown || unit == HeightUnits.Unknown)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
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
        await _unitOfWork.SaveChangesAsync();
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

    public Task<List<MeasurementEntity>> GetMeasurementsListAsync(UserEntity currentUser, Guid routeUserId, BodyParts? bodyPart, HeightUnits? unit)
    {
        return GetMeasurementsInternalAsync(currentUser, routeUserId, bodyPart, unit, orderAscending: false);
    }

    public Task<List<MeasurementEntity>> GetMeasurementsHistoryAsync(UserEntity currentUser, Guid routeUserId, BodyParts? bodyPart, HeightUnits? unit)
    {
        return GetMeasurementsInternalAsync(currentUser, routeUserId, bodyPart, unit, orderAscending: true);
    }

    public async Task<MeasurementTrendResult> GetMeasurementsTrendAsync(
        UserEntity currentUser,
        Guid routeUserId,
        BodyParts bodyPart,
        HeightUnits unit)
    {
        ValidateAccess(currentUser, routeUserId);

        if (!System.Enum.IsDefined(bodyPart) || bodyPart == BodyParts.Unknown)
        {
            throw AppException.BadRequest(Messages.BodyPartRequired);
        }

        if (!System.Enum.IsDefined(unit) || unit == HeightUnits.Unknown)
        {
            throw AppException.BadRequest(Messages.UnitRequired);
        }

        var measurements = await _measurementRepository.GetByUserAsync(currentUser.Id, bodyPart);
        if (measurements.Count < 1)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var ordered = measurements.OrderBy(m => m.CreatedAt).ToList();
        var convertedValues = ordered
            .Select(m => ConvertValue(m.Value, ParseHeightUnit(m.Unit), unit))
            .ToList();

        var startValue = convertedValues[0];
        var currentValue = convertedValues[^1];
        var change = currentValue - startValue;
        var roundedChange = Math.Round(change, 2);
        var changePercentage = startValue == 0d ? 0d : (change / startValue) * 100d;

        return new MeasurementTrendResult
        {
            BodyPart = bodyPart,
            Unit = unit,
            StartValue = Math.Round(startValue, 2),
            CurrentValue = Math.Round(currentValue, 2),
            Change = roundedChange,
            ChangePercentage = Math.Round(changePercentage, 2),
            Direction = ResolveDirection(roundedChange),
            Points = ordered.Count
        };
    }

    private async Task<List<MeasurementEntity>> GetMeasurementsInternalAsync(
        UserEntity currentUser,
        Guid routeUserId,
        BodyParts? bodyPart,
        HeightUnits? unit,
        bool orderAscending)
    {
        ValidateAccess(currentUser, routeUserId);

        if (bodyPart.HasValue && (!System.Enum.IsDefined(bodyPart.Value) || bodyPart.Value == BodyParts.Unknown))
        {
            throw AppException.BadRequest(Messages.BodyPartRequired);
        }

        if (unit.HasValue && (!System.Enum.IsDefined(unit.Value) || unit.Value == HeightUnits.Unknown))
        {
            throw AppException.BadRequest(Messages.UnitRequired);
        }

        var measurements = await _measurementRepository.GetByUserAsync(currentUser.Id, bodyPart);
        if (measurements.Count < 1)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var ordered = orderAscending
            ? measurements.OrderBy(m => m.CreatedAt)
            : measurements.OrderByDescending(m => m.CreatedAt);

        return ordered.Select(m =>
        {
            var parsedUnit = ParseHeightUnit(m.Unit);
            var targetUnit = unit ?? parsedUnit;
            var convertedValue = ConvertValue(m.Value, parsedUnit, targetUnit);

            return new MeasurementEntity
            {
                Id = m.Id,
                UserId = m.UserId,
                BodyPart = m.BodyPart,
                Unit = targetUnit.ToString(),
                Value = Math.Round(convertedValue, 2),
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt
            };
        }).ToList();
    }

    private static void ValidateAccess(UserEntity currentUser, Guid routeUserId)
    {
        if (currentUser == null || routeUserId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (currentUser.Id != routeUserId)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }
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

        throw AppException.BadRequest(Messages.UnitRequired);
    }

    private double ConvertValue(double value, HeightUnits fromUnit, HeightUnits toUnit)
    {
        return _heightUnitConverter.Convert(value, fromUnit, toUnit);
    }
}
