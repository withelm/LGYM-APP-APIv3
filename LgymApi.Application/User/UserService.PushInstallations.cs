using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.User.Models;
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

        var installation = await _pushInstallationRepository.FindByInstallationIdAsync(normalizedInstallationId, cancellationToken);
        var isNewInstallation = installation == null;
        if (installation == null)
        {
            installation = new PushInstallation
            {
                Id = Id<PushInstallation>.New(),
                InstallationId = normalizedInstallationId
            };

            await _pushInstallationRepository.AddAsync(installation, cancellationToken);
        }

        installation.UserId = currentUser.Id;
        installation.SessionId = sessionId.Value;
        installation.InstallationId = normalizedInstallationId;
        installation.Platform = normalizedPlatform;
        installation.FcmToken = normalizedToken;
        installation.AppVersion = NormalizeOptionalValue(input.AppVersion);
        installation.Environment = normalizedEnvironment;
        installation.PermissionStatus = NormalizeOptionalValue(input.PermissionStatus);
        installation.LastSeenAt = DateTimeOffset.UtcNow;
        installation.DisabledAt = null;
        installation.DisabledReason = null;

        if (!isNewInstallation)
        {
            await _pushInstallationRepository.UpdateAsync(installation, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> UnregisterPushInstallationAsync(
        UserEntity? currentUser,
        Id<UserSessionEntity>? sessionId,
        PushInstallationActionInput input,
        CancellationToken cancellationToken = default)
    {
        var installation = await GetBoundInstallationAsync(currentUser, sessionId, input, cancellationToken);
        if (installation.IsFailure)
        {
            return Result<Unit, AppError>.Failure(installation.Error);
        }

        if (installation.Value == null)
        {
            return Result<Unit, AppError>.Success(Unit.Value);
        }

        installation.Value.DisabledAt = DateTimeOffset.UtcNow;
        installation.Value.DisabledReason = UnregisteredDisabledReason;
        installation.Value.LastSeenAt = DateTimeOffset.UtcNow;

        await _pushInstallationRepository.UpdateAsync(installation.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> DisassociatePushInstallationAsync(
        UserEntity? currentUser,
        Id<UserSessionEntity>? sessionId,
        PushInstallationActionInput input,
        CancellationToken cancellationToken = default)
    {
        var installation = await GetBoundInstallationAsync(currentUser, sessionId, input, cancellationToken);
        if (installation.IsFailure)
        {
            return Result<Unit, AppError>.Failure(installation.Error);
        }

        if (installation.Value == null)
        {
            return Result<Unit, AppError>.Success(Unit.Value);
        }

        DisassociateInstallation(installation.Value);
        await _pushInstallationRepository.UpdateAsync(installation.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private async Task<Result<PushInstallation?, AppError>> GetBoundInstallationAsync(
        UserEntity? currentUser,
        Id<UserSessionEntity>? sessionId,
        PushInstallationActionInput input,
        CancellationToken cancellationToken)
    {
        if (currentUser == null || !sessionId.HasValue)
        {
            return Result<PushInstallation?, AppError>.Failure(new UserUnauthorizedError(Messages.Unauthorized));
        }

        var normalizedInstallationId = NormalizeRequiredValue(input.InstallationKey);
        if (normalizedInstallationId == null)
        {
            return Result<PushInstallation?, AppError>.Failure(new InvalidUserError(Messages.FieldRequired));
        }

        var installation = await _pushInstallationRepository.FindBoundToUserOrSessionAsync(
            normalizedInstallationId,
            currentUser.Id,
            sessionId.Value,
            cancellationToken);

        return Result<PushInstallation?, AppError>.Success(installation);
    }

    private async Task DisassociateInstallationsForSessionAsync(Id<UserSessionEntity> sessionId, CancellationToken cancellationToken)
    {
        var installations = await _pushInstallationRepository.GetBySessionIdAsync(sessionId, cancellationToken);
        foreach (var installation in installations)
        {
            DisassociateInstallation(installation);
        }
    }

    private static void DisassociateInstallation(PushInstallation installation)
    {
        installation.UserId = null;
        installation.SessionId = null;
        installation.LastSeenAt = DateTimeOffset.UtcNow;
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
