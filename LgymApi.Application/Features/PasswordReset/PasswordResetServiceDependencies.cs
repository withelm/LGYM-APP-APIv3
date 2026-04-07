using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;

namespace LgymApi.Application.Features.PasswordReset;

public sealed class PasswordResetServiceDependencies
{
    public required IUserRepository UserRepository { get; init; }
    public required IPasswordResetTokenRepository PasswordResetTokenRepository { get; init; }
    public required IPasswordResetTokenGenerationService TokenGenerationService { get; init; }
    public required ILegacyPasswordService LegacyPasswordService { get; init; }
    public required IEmailScheduler<PasswordRecoveryEmailPayload> PasswordRecoveryEmailScheduler { get; init; }
    public required IUserSessionCache UserSessionCache { get; init; }
    public required IUnitOfWork UnitOfWork { get; init; }
}
