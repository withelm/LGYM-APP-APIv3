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

    public MeasurementsService(IMeasurementsServiceDependencies dependencies)
    {
        _measurementRepository = dependencies.MeasurementRepository;
        _roleRepository = dependencies.RoleRepository;
        _trainerRelationshipRepository = dependencies.TrainerRelationshipRepository;
        _heightUnitConverter = dependencies.HeightUnitConverter;
        _weightUnitConverter = dependencies.WeightUnitConverter;
        _unitOfWork = dependencies.UnitOfWork;
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

        if (value <= 0)
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
        => GetMeasurementsInternalAsync(currentUser, routeUserId, bodyPart, unit, orderAscending: false, cancellationToken);

    public Task<Result<List<MeasurementEntity>, AppError>> GetMeasurementsHistoryAsync(UserEntity currentUser, Id<UserEntity> routeUserId, BodyParts? bodyPart, MeasurementUnits? unit, CancellationToken cancellationToken = default)
        => GetMeasurementsInternalAsync(currentUser, routeUserId, bodyPart, unit, orderAscending: true, cancellationToken);

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
            return Result<MeasurementTrendResult, AppError>.Success(MeasurementTrendCalculator.CreateInsufficientTrend(bodyPart, unit, 0));
        }

        var ordered = measurements.OrderBy(m => m.CreatedAt).ToList();
        return MeasurementTrendCalculator.BuildTrendResult(ordered, bodyPart, unit, ConvertValue);
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
            var defaultUnit = MeasurementTrendCalculator.ResolveTrendTargetUnit(ordered);
            var trendResult = MeasurementTrendCalculator.BuildTrendResult(ordered, groupedMeasurements.Key, defaultUnit, ConvertValue);
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

        return MeasurementListProjector.ProjectMeasurements(ordered, unit, ConvertValue);
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
