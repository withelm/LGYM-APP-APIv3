using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.WorkoutProgress.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Features.Measurements.Models;
using LgymApi.Application.WorkoutProgress.Contracts.Measurements;
using LgymApi.Application.WorkoutProgress.ProgressData;
using LgymApi.Application.WorkoutProgress.ProgressData.Models;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Domain.Security;
using LgymApi.Resources;
using LgymApi.Application.WorkoutProgress.Authorization;
using LgymApi.Application.WorkoutProgress.Persistence;

namespace LgymApi.Application.Features.Measurements;

public sealed class MeasurementsService : IMeasurementsService
{
    private readonly IWorkoutProgressReadWriteService _progress;
    private readonly IAccountAccessReader _accountAccess;
    private readonly IMeasurementsRelationshipAccessPort _relationshipAccess;
    private readonly IWorkoutProgressOwnershipAuthorizationService _ownershipAuthorization;

    public MeasurementsService(
        IWorkoutProgressReadWriteService progress,
        IAccountAccessReader accountAccess,
        IMeasurementsRelationshipAccessPort relationshipAccess,
        IWorkoutProgressOwnershipAuthorizationService ownershipAuthorization)
    {
        _progress = progress;
        _accountAccess = accountAccess;
        _relationshipAccess = relationshipAccess;
        _ownershipAuthorization = ownershipAuthorization;
    }

    public Task<Result<Unit, AppError>> AddMeasurementAsync(AuthenticatedAccountContext? currentUser, BodyParts bodyPart, MeasurementUnits unit, double value, CancellationToken cancellationToken = default)
        => _progress.AddMeasurementAsync(currentUser?.Id ?? Id<AccountReference>.Empty, bodyPart, unit, value, cancellationToken);

    public Task<Result<Unit, AppError>> AddMeasurementsAsync(AuthenticatedAccountContext? currentUser, IReadOnlyCollection<MeasurementCreateInput> measurements, CancellationToken cancellationToken = default)
        => _progress.AddMeasurementsAsync(currentUser?.Id ?? Id<AccountReference>.Empty, measurements.Select(item => new MeasurementWriteModel(item.BodyPart, item.Unit, item.Value)).ToList(), cancellationToken);

    public async Task<Result<MeasurementReadModel, AppError>> GetMeasurementDetailAsync(AuthenticatedAccountContext? currentUser, Id<LgymApi.Domain.Entities.Measurement> measurementId, CancellationToken cancellationToken = default)
    {
        if (measurementId.IsEmpty)
        {
            return Result<MeasurementReadModel, AppError>.Failure(new InvalidMeasurementError(Messages.InvalidId));
        }

        var owner = await _progress.GetMeasurementOwnerAsync(measurementId, cancellationToken);
        if (owner.IsFailure)
        {
            return Result<MeasurementReadModel, AppError>.Failure(owner.Error);
        }

        var access = await GetAccessAsync(currentUser, owner.Value, cancellationToken);
        return access.IsFailure ? Result<MeasurementReadModel, AppError>.Failure(access.Error) : await _progress.GetMeasurementDetailForOwnerAsync(owner.Value, measurementId, cancellationToken, access.Value);
    }

    public async Task<Result<List<MeasurementReadModel>, AppError>> GetMeasurementsListAsync(AuthenticatedAccountContext? currentUser, Id<AccountReference> routeUserId, BodyParts? bodyPart, MeasurementUnits? unit, CancellationToken cancellationToken = default)
    {
        var access = await GetAccessAsync(currentUser, routeUserId, cancellationToken);
        return access.IsFailure ? Result<List<MeasurementReadModel>, AppError>.Failure(access.Error) : await _progress.GetMeasurementsListForOwnerAsync(routeUserId, bodyPart, unit, cancellationToken, access.Value);
    }

    public async Task<Result<List<MeasurementReadModel>, AppError>> GetMeasurementsHistoryAsync(AuthenticatedAccountContext? currentUser, Id<AccountReference> routeUserId, BodyParts? bodyPart, MeasurementUnits? unit, CancellationToken cancellationToken = default)
    {
        var access = await GetAccessAsync(currentUser, routeUserId, cancellationToken);
        return access.IsFailure ? Result<List<MeasurementReadModel>, AppError>.Failure(access.Error) : await _progress.GetMeasurementsHistoryForOwnerAsync(routeUserId, bodyPart, unit, cancellationToken, access.Value);
    }

    public async Task<Result<MeasurementTrendReadModel, AppError>> GetMeasurementsTrendAsync(AuthenticatedAccountContext? currentUser, Id<AccountReference> routeUserId, BodyParts bodyPart, MeasurementUnits unit, CancellationToken cancellationToken = default)
    {
        var access = await GetAccessAsync(currentUser, routeUserId, cancellationToken);
        return access.IsFailure ? Result<MeasurementTrendReadModel, AppError>.Failure(access.Error) : await _progress.GetMeasurementsTrendForOwnerAsync(routeUserId, bodyPart, unit, cancellationToken, access.Value);
    }

    public async Task<Result<List<MeasurementTrendReadModel>, AppError>> GetMeasurementsTrendsAsync(AuthenticatedAccountContext? currentUser, Id<AccountReference> routeUserId, CancellationToken cancellationToken = default)
    {
        var access = await GetAccessAsync(currentUser, routeUserId, cancellationToken);
        return access.IsFailure ? Result<List<MeasurementTrendReadModel>, AppError>.Failure(access.Error) : await _progress.GetMeasurementsTrendsForOwnerAsync(routeUserId, cancellationToken, access.Value);
    }

    private async Task<Result<MeasurementReadScope?, AppError>> GetAccessAsync(AuthenticatedAccountContext? currentUser, Id<AccountReference> routeUserId, CancellationToken cancellationToken)
    {
        if (currentUser == null || routeUserId.IsEmpty)
        {
            return Result<MeasurementReadScope?, AppError>.Failure(new InvalidMeasurementError(Messages.InvalidId));
        }

        var subject = await _accountAccess.GetByIdAsync(routeUserId, cancellationToken);
        if (subject is null || subject.IsDeleted)
        {
            return Result<MeasurementReadScope?, AppError>.Failure(new MeasurementNotFoundError(Messages.DidntFind));
        }

        if (_ownershipAuthorization.IsOwner(currentUser, routeUserId))
        {
            return Result<MeasurementReadScope?, AppError>.Success(null);
        }

        var account = await _accountAccess.GetByIdAsync(currentUser.Id, cancellationToken);
        if (account?.Roles.Contains(AuthConstants.Roles.Trainer, StringComparer.Ordinal) != true)
        {
            return Result<MeasurementReadScope?, AppError>.Failure(new MeasurementForbiddenError(Messages.Forbidden));
        }

        var relationshipAccess = await _relationshipAccess.GetAccessAsync(currentUser.Id, routeUserId, cancellationToken);
        if (relationshipAccess.HasActiveRelationship)
        {
            return Result<MeasurementReadScope?, AppError>.Success(null);
        }

        var scope = new MeasurementReadScope(relationshipAccess.Periods);
        return relationshipAccess.Periods.Count == 0
            ? Result<MeasurementReadScope?, AppError>.Failure(new MeasurementForbiddenError(Messages.Forbidden))
            : Result<MeasurementReadScope?, AppError>.Success(scope);
    }
}
