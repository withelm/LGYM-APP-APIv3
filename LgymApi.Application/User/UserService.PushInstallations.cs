using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.User.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using UserEntity = LgymApi.Domain.Entities.User;
using UserSessionEntity = LgymApi.Domain.Entities.UserSession;

namespace LgymApi.Application.Features.User;

public sealed partial class UserService : IUserService
{
    private const string UnregisteredDisabledReason = "Unregistered";

    public async Task<Result<Unit, AppError>> RegisterPushInstallationAsync(
        UserEntity? currentUser,
        Id<UserSessionEntity>? sessionId,
        RegisterPushInstallationInput input,
        CancellationToken cancellationToken = default)
    {
        if (currentUser == null || !sessionId.HasValue)
        {
            return Result<Unit, AppError>.Failure(new UserUnauthorizedError(Messages.Unauthorized));
        }

        var normalizedInstallationId = NormalizeRequiredValue(input.InstallationKey);
        var normalizedPlatform = NormalizeRequiredValue(input.Platform);
        var normalizedToken = NormalizeRequiredValue(input.FcmToken);
        var normalizedEnvironment = NormalizeRequiredValue(input.Environment);

        if (normalizedInstallationId == null
            || normalizedPlatform == null
            || normalizedToken == null
            || normalizedEnvironment == null)
        {
            return Result<Unit, AppError>.Failure(new InvalidUserError(Messages.FieldRequired));
        }

        await _pushInstallationRepository.UpsertForUserSessionAsync(
            new PushInstallationRegistration(
                normalizedInstallationId,
                normalizedPlatform,
                normalizedToken,
                NormalizeOptionalValue(input.AppVersion),
                normalizedEnvironment,
                NormalizeOptionalValue(input.PermissionStatus),
                currentUser.Id,
                sessionId.Value,
                DateTimeOffset.UtcNow),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> UnregisterPushInstallationAsync(
        UserEntity? currentUser,
        Id<UserSessionEntity>? sessionId,
        PushInstallationActionInput input,
        CancellationToken cancellationToken = default)
    {
        if (currentUser == null || !sessionId.HasValue)
        {
            return Result<Unit, AppError>.Failure(new UserUnauthorizedError(Messages.Unauthorized));
        }

        var normalizedInstallationId = NormalizeRequiredValue(input.InstallationKey);
        if (normalizedInstallationId == null)
        {
            return Result<Unit, AppError>.Failure(new InvalidUserError(Messages.FieldRequired));
        }

        await _pushInstallationRepository.DisableBoundForUserOrSessionAsync(
            normalizedInstallationId,
            currentUser.Id,
            sessionId.Value,
            DateTimeOffset.UtcNow,
            UnregisteredDisabledReason,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> DisassociatePushInstallationAsync(
        UserEntity? currentUser,
        Id<UserSessionEntity>? sessionId,
        PushInstallationActionInput input,
        CancellationToken cancellationToken = default)
    {
        if (currentUser == null || !sessionId.HasValue)
        {
            return Result<Unit, AppError>.Failure(new UserUnauthorizedError(Messages.Unauthorized));
        }

        var normalizedInstallationId = NormalizeRequiredValue(input.InstallationKey);
        if (normalizedInstallationId == null)
        {
            return Result<Unit, AppError>.Failure(new InvalidUserError(Messages.FieldRequired));
        }

        await _pushInstallationRepository.DisassociateBoundForUserOrSessionAsync(
            normalizedInstallationId,
            currentUser.Id,
            sessionId.Value,
            DateTimeOffset.UtcNow,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private async Task DisassociateInstallationsForSessionAsync(Id<UserSessionEntity> sessionId, CancellationToken cancellationToken)
    {
        await _pushInstallationRepository.DisassociateForSessionAsync(sessionId, DateTimeOffset.UtcNow, cancellationToken);
    }

    private static string? NormalizeRequiredValue(string? value)
    {
        var normalized = NormalizeOptionalValue(value);
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
