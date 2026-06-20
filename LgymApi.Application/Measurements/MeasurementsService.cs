using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.Measurements.Models;
using LgymApi.Application.Repositories;
using LgymApi.Application.Units;
using LgymApi.Domain.ValueObjects;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
using LgymApi.Resources;
using MeasurementEntity = LgymApi.Domain.Entities.Measurement;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Measurements;

public sealed class MeasurementsService : IMeasurementsService
{
    private readonly IMeasurementRepository _measurementRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly ITrainerRelationshipRepository _trainerRelationshipRepository;
    private readonly IUnitConverter<HeightUnits> _heightUnitConverter;
    private readonly IUnitConverter<WeightUnits> _weightUnitConverter;
    private readonly IUnitOfWork _unitOfWork;

    public MeasurementsService(
        IMeasurementRepository measurementRepository,
        IRoleRepository roleRepository,
        ITrainerRelationshipRepository trainerRelationshipRepository,
        IUnitConverter<HeightUnits> heightUnitConverter,
        IUnitConverter<WeightUnits> weightUnitConverter,
        IUnitOfWork unitOfWork)
    {
        _measurementRepository = measurementRepository;
        _roleRepository = roleRepository;
        _trainerRelationshipRepository = trainerRelationshipRepository;
        _heightUnitConverter = heightUnitConverter;
        _weightUnitConverter = weightUnitConverter;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit, AppError>> AddMeasurementAsync(UserEntity currentUser, BodyParts bodyPart, MeasurementUnits unit, double value, CancellationToken cancellationToken = default)
    {
        if (currentUser == null)
        {
            return Result<Unit, AppError>.Failure(new InvalidMeasurementError(Messages.InvalidId));
        }

        if (!MeasurementUnitResolver.IsUnitAllowedForBodyPart(bodyPart, unit))
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

    public async Task<Result<Unit, AppError>> AddMeasurementsAsync(UserEntity currentUser, IReadOnlyCollection<MeasurementCreateInput> measurements, CancellationToken cancellationToken = default)
    {
        if (currentUser == null)
        {
            return Result<Unit, AppError>.Failure(new InvalidMeasurementError(Messages.InvalidId));
        }

        if (measurements == null || measurements.Count == 0)
        {
            return Result<Unit, AppError>.Failure(new InvalidMeasurementError(Messages.FieldRequired));
        }

        foreach (var measurementInput in measurements)
        {
            if (!MeasurementUnitResolver.IsUnitAllowedForBodyPart(measurementInput.BodyPart, measurementInput.Unit) || measurementInput.Value <= 0)
            {
                return Result<Unit, AppError>.Failure(new InvalidMeasurementError(Messages.FieldRequired));
            }

            var measurement = new MeasurementEntity
            {
                Id = Id<MeasurementEntity>.New(),
                UserId = currentUser.Id,
                BodyPart = measurementInput.BodyPart,
                Unit = measurementInput.Unit.ToString(),
                Value = measurementInput.Value
            };

            await _measurementRepository.AddAsync(measurement, cancellationToken);
        }

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
            return Result<MeasurementEntity, AppError>.Failure(new InvalidMeasurementError(Messages.InvalidId));
        }

        var measurement = await _measurementRepository.FindByIdAsync(measurementId, cancellationToken);
        if (measurement == null)
        {
            return Result<MeasurementEntity, AppError>.Failure(new MeasurementNotFoundError(Messages.DidntFind));
        }

        var accessValidation = await ValidateAccessAsync(currentUser, measurement.UserId, cancellationToken);
        if (accessValidation.IsFailure)
        {
            return Result<MeasurementEntity, AppError>.Failure(accessValidation.Error);
        }

        return Result<MeasurementEntity, AppError>.Success(measurement);
    }

    public Task<Result<List<MeasurementEntity>, AppError>> GetMeasurementsListAsync(UserEntity currentUser, Id<UserEntity> routeUserId, BodyParts? bodyPart, MeasurementUnits? unit, CancellationToken cancellationToken = default)
    {
        return GetMeasurementsInternalAsync(currentUser, routeUserId, bodyPart, unit, orderAscending: false, cancellationToken);
    }

    public Task<Result<List<MeasurementEntity>, AppError>> GetMeasurementsHistoryAsync(UserEntity currentUser, Id<UserEntity> routeUserId, BodyParts? bodyPart, MeasurementUnits? unit, CancellationToken cancellationToken = default)
    {
        return GetMeasurementsInternalAsync(currentUser, routeUserId, bodyPart, unit, orderAscending: true, cancellationToken);
    }

    public async Task<Result<MeasurementTrendResult, AppError>> GetMeasurementsTrendAsync(
        UserEntity currentUser,
        Id<UserEntity> routeUserId,
        BodyParts bodyPart,
        MeasurementUnits unit,
        CancellationToken cancellationToken = default)
    {
        var accessValidation = await ValidateAccessAsync(currentUser, routeUserId, cancellationToken);
        if (accessValidation.IsFailure)
        {
            return Result<MeasurementTrendResult, AppError>.Failure(accessValidation.Error);
        }

        if (!System.Enum.IsDefined(bodyPart) || bodyPart == BodyParts.Unknown)
        {
            return Result<MeasurementTrendResult, AppError>.Failure(new InvalidMeasurementError(Messages.BodyPartRequired));
        }

        if (!MeasurementUnitResolver.IsUnitAllowedForBodyPart(bodyPart, unit))
        {
            return Result<MeasurementTrendResult, AppError>.Failure(new InvalidMeasurementError(Messages.UnitRequired));
        }

        var measurements = await _measurementRepository.GetByUserAsync(routeUserId, bodyPart, cancellationToken);
        if (measurements.Count < 1)
        {
            return Result<MeasurementTrendResult, AppError>.Success(CreateInsufficientTrend(bodyPart, unit, 0));
        }

        var ordered = measurements.OrderBy(m => m.CreatedAt).ToList();
        if (ordered.Count == 1)
        {
            var parsedSingleUnit = ParseMeasurementUnit(ordered[0].Unit);
            if (parsedSingleUnit == MeasurementUnits.Unknown)
            {
                return Result<MeasurementTrendResult, AppError>.Failure(new InvalidMeasurementError(Messages.UnitRequired));
            }

            var singleValue = ConvertValue(ordered[0].Value, parsedSingleUnit, unit, bodyPart);
            return Result<MeasurementTrendResult, AppError>.Success(new MeasurementTrendResult
            {
                BodyPart = bodyPart,
                Unit = unit,
                StartValue = Math.Round(singleValue, 2),
                CurrentValue = Math.Round(singleValue, 2),
                Change = 0d,
                ChangePercentage = 0d,
                FirstMeasurementValue = Math.Round(singleValue, 2),
                FirstMeasurementDate = ordered[0].CreatedAt,
                LastMeasurementValue = Math.Round(singleValue, 2),
                LastMeasurementDate = ordered[0].CreatedAt,
                Difference = 0d,
                Direction = "insufficient_data",
                Points = 1
            });
        }

        var convertedValues = new List<double>();
        foreach (var measurement in ordered)
        {
            var parsedUnit = ParseMeasurementUnit(measurement.Unit);
            if (parsedUnit == MeasurementUnits.Unknown)
            {
                return Result<MeasurementTrendResult, AppError>.Failure(new InvalidMeasurementError(Messages.UnitRequired));
            }

            convertedValues.Add(ConvertValue(measurement.Value, parsedUnit, unit, bodyPart));
        }

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
            FirstMeasurementValue = Math.Round(startValue, 2),
            FirstMeasurementDate = ordered[0].CreatedAt,
            LastMeasurementValue = Math.Round(currentValue, 2),
            LastMeasurementDate = ordered[^1].CreatedAt,
            Difference = Math.Round(Math.Abs(roundedChange), 2),
            Direction = ResolveDirection(roundedChange),
            Points = ordered.Count
        });
    }

    public async Task<Result<List<MeasurementTrendResult>, AppError>> GetMeasurementsTrendsAsync(
        UserEntity currentUser,
        Id<UserEntity> routeUserId,
        CancellationToken cancellationToken = default)
    {
        var accessValidation = await ValidateAccessAsync(currentUser, routeUserId, cancellationToken);
        if (accessValidation.IsFailure)
        {
            return Result<List<MeasurementTrendResult>, AppError>.Failure(accessValidation.Error);
        }

        var measurements = await _measurementRepository.GetByUserAsync(routeUserId, null, cancellationToken);
        if (measurements.Count < 1)
        {
            return Result<List<MeasurementTrendResult>, AppError>.Success(new List<MeasurementTrendResult>());
        }

        var trends = new List<MeasurementTrendResult>();
        foreach (var groupedMeasurements in measurements.GroupBy(m => m.BodyPart).OrderBy(group => group.Key.ToString()))
        {
            var ordered = groupedMeasurements.OrderBy(m => m.CreatedAt).ToList();
            var defaultUnit = ResolveTrendTargetUnit(ordered);
            var trendResult = await GetMeasurementsTrendAsync(currentUser, routeUserId, groupedMeasurements.Key, defaultUnit, cancellationToken);
            if (trendResult.IsFailure)
            {
                return Result<List<MeasurementTrendResult>, AppError>.Failure(trendResult.Error);
            }

            trends.Add(trendResult.Value);
        }

        return Result<List<MeasurementTrendResult>, AppError>.Success(trends);
    }

    private async Task<Result<List<MeasurementEntity>, AppError>> GetMeasurementsInternalAsync(
        UserEntity currentUser,
        Id<UserEntity> routeUserId,
        BodyParts? bodyPart,
        MeasurementUnits? unit,
        bool orderAscending,
        CancellationToken cancellationToken)
    {
        var accessValidation = await ValidateAccessAsync(currentUser, routeUserId, cancellationToken);
        if (accessValidation.IsFailure)
        {
            return Result<List<MeasurementEntity>, AppError>.Failure(accessValidation.Error);
        }

        if (bodyPart.HasValue && (!System.Enum.IsDefined(bodyPart.Value) || bodyPart.Value == BodyParts.Unknown))
        {
            return Result<List<MeasurementEntity>, AppError>.Failure(new InvalidMeasurementError(Messages.BodyPartRequired));
        }

        if (unit.HasValue && (!bodyPart.HasValue || !MeasurementUnitResolver.IsUnitAllowedForBodyPart(bodyPart.Value, unit.Value)))
        {
            return Result<List<MeasurementEntity>, AppError>.Failure(new InvalidMeasurementError(Messages.UnitRequired));
        }

        var measurements = await _measurementRepository.GetByUserAsync(routeUserId, bodyPart, cancellationToken);
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
            var parsedUnit = ParseMeasurementUnit(m.Unit);
            if (parsedUnit == MeasurementUnits.Unknown)
            {
                return Result<List<MeasurementEntity>, AppError>.Failure(new InvalidMeasurementError(Messages.UnitRequired));
            }

            var targetUnit = unit ?? parsedUnit;
            if (!MeasurementUnitResolver.IsUnitAllowedForBodyPart(m.BodyPart, targetUnit))
            {
                return Result<List<MeasurementEntity>, AppError>.Failure(new InvalidMeasurementError(Messages.UnitRequired));
            }

            var convertedValue = ConvertValue(m.Value, parsedUnit, targetUnit, m.BodyPart);

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

    private async Task<Result<Unit, AppError>> ValidateAccessAsync(UserEntity currentUser, Id<UserEntity> routeUserId, CancellationToken cancellationToken)
    {
        if (currentUser == null || routeUserId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidMeasurementError(Messages.InvalidId));
        }

        if (currentUser.Id == routeUserId)
        {
            return Result<Unit, AppError>.Success(Unit.Value);
        }

        var isTrainer = await _roleRepository.UserHasRoleAsync(currentUser.Id, AuthConstants.Roles.Trainer, cancellationToken);
        if (!isTrainer)
        {
            return Result<Unit, AppError>.Failure(new MeasurementForbiddenError(Messages.Forbidden));
        }

        var link = await _trainerRelationshipRepository.FindActiveLinkByTrainerAndTraineeAsync(currentUser.Id, routeUserId, cancellationToken);
        if (link == null)
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

        return "same";
    }

    private static MeasurementTrendResult CreateInsufficientTrend(BodyParts bodyPart, MeasurementUnits unit, int points)
    {
        return new MeasurementTrendResult
        {
            BodyPart = bodyPart,
            Unit = unit,
            Direction = "insufficient_data",
            Points = points
        };
    }

    private static MeasurementUnits ParseMeasurementUnit(string? unit)
    {
        return MeasurementUnitResolver.TryParseStoredUnit(unit, out var parsedUnit)
            ? parsedUnit
            : MeasurementUnits.Unknown;
    }

    private MeasurementUnits ResolveTrendTargetUnit(List<MeasurementEntity> measurements)
    {
        var latestUnit = ParseMeasurementUnit(measurements[^1].Unit);
        if (latestUnit != MeasurementUnits.Unknown)
        {
            return latestUnit;
        }

        return MeasurementUnitResolver.GetDefaultUnit(measurements[^1].BodyPart);
    }

    private double ConvertValue(double value, MeasurementUnits fromUnit, MeasurementUnits toUnit, BodyParts bodyPart)
    {
        if (fromUnit == toUnit)
        {
            return value;
        }

        if (MeasurementUnitResolver.IsWeightMeasurement(bodyPart))
        {
            if (!MeasurementUnitResolver.TryGetWeightUnit(fromUnit, out var fromWeightUnit) ||
                !MeasurementUnitResolver.TryGetWeightUnit(toUnit, out var toWeightUnit))
            {
                return value;
            }

            return _weightUnitConverter.Convert(value, fromWeightUnit, toWeightUnit);
        }

        if (MeasurementUnitResolver.IsLengthMeasurement(bodyPart))
        {
            if (!MeasurementUnitResolver.TryGetHeightUnit(fromUnit, out var fromHeightUnit) ||
                !MeasurementUnitResolver.TryGetHeightUnit(toUnit, out var toHeightUnit))
            {
                return value;
            }

            return _heightUnitConverter.Convert(value, fromHeightUnit, toHeightUnit);
        }

        return value;
    }
}
