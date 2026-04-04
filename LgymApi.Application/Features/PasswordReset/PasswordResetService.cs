using System.Security.Cryptography;
using System.Text;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;

using Result = LgymApi.Application.Common.Results.Result<LgymApi.Application.Common.Results.Unit, LgymApi.Application.Common.Errors.AppError>;

namespace LgymApi.Application.Features.PasswordReset;

public sealed class PasswordResetService : IPasswordResetService
{
    private const int ResetTokenExpiryMinutes = 30;

    private readonly IUserRepository _userRepository;
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly IPasswordResetTokenGenerationService _tokenGenerationService;
    private readonly ILegacyPasswordService _legacyPasswordService;
    private readonly IEmailScheduler<PasswordRecoveryEmailPayload> _passwordRecoveryEmailScheduler;
    private readonly IUserSessionCache _userSessionCache;
    private readonly IUnitOfWork _unitOfWork;

    public PasswordResetService(
        IUserRepository userRepository,
        IPasswordResetTokenRepository passwordResetTokenRepository,
        IPasswordResetTokenGenerationService tokenGenerationService,
        ILegacyPasswordService legacyPasswordService,
        IEmailScheduler<PasswordRecoveryEmailPayload> passwordRecoveryEmailScheduler,
        IUserSessionCache userSessionCache,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _passwordResetTokenRepository = passwordResetTokenRepository;
        _tokenGenerationService = tokenGenerationService;
        _legacyPasswordService = legacyPasswordService;
        _passwordRecoveryEmailScheduler = passwordRecoveryEmailScheduler;
        _userSessionCache = userSessionCache;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> RequestPasswordResetAsync(string email, string cultureName, CancellationToken ct)
    {
        var emailValueObject = new Email(email);
        var user = await _userRepository.FindByEmailAsync(emailValueObject, ct);

        if (user == null || user.IsDeleted)
        {
            return Result.Success(Unit.Value);
        }

        var activeTokens = await _passwordResetTokenRepository.GetActiveForUserAsync(user.Id, ct);
        foreach (var activeToken in activeTokens)
        {
            activeToken.IsUsed = true;
            await _passwordResetTokenRepository.UpdateAsync(activeToken, ct);
        }

        var generatedToken = await _tokenGenerationService.GenerateUniqueAsync(ct);

        var resetToken = new PasswordResetToken
        {
            Id = LgymApi.Domain.ValueObjects.Id<PasswordResetToken>.New(),
            UserId = user.Id,
            TokenHash = generatedToken.TokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(ResetTokenExpiryMinutes),
            IsUsed = false
        };

        await _passwordResetTokenRepository.AddAsync(resetToken, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await _passwordRecoveryEmailScheduler.ScheduleAsync(new PasswordRecoveryEmailPayload
        {
            UserId = user.Id,
            TokenId = resetToken.Id,
            UserName = user.Name,
            RecipientEmail = user.Email.Value,
            ResetToken = generatedToken.PlainTextToken,
            CultureName = cultureName
        }, ct);

        return Result.Success(Unit.Value);
    }

    public async Task<Result> ResetPasswordAsync(string plainTextToken, string newPassword, CancellationToken ct)
    {
        var tokenHash = ComputeSha256Hex(plainTextToken);
        var resetToken = await _passwordResetTokenRepository.FindActiveByTokenHashAsync(tokenHash, ct);
        if (resetToken == null)
        {
            return Result.Failure(new InvalidUserError(Messages.InvalidToken));
        }

        var user = await _userRepository.FindByIdAsync(resetToken.UserId, ct);
        if (user == null || user.IsDeleted)
        {
            return Result.Failure(new InvalidUserError(Messages.InvalidToken));
        }

        var passwordData = _legacyPasswordService.Create(newPassword);
        user.LegacyHash = passwordData.Hash;
        user.LegacySalt = passwordData.Salt;
        user.LegacyIterations = passwordData.Iterations;
        user.LegacyKeyLength = passwordData.KeyLength;
        user.LegacyDigest = passwordData.Digest;

        resetToken.IsUsed = true;

        await _userRepository.UpdateAsync(user, ct);
        await _passwordResetTokenRepository.UpdateAsync(resetToken, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _userSessionCache.Remove(user.Id);

        return Result.Success(Unit.Value);
    }

    private static string ComputeSha256Hex(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
