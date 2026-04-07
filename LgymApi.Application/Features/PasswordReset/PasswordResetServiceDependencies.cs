using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;

namespace LgymApi.Application.Features.PasswordReset;

public sealed class PasswordResetServiceDependencies
{
    public PasswordResetServiceDependencies(
        IUserRepository userRepository,
        IPasswordResetTokenRepository passwordResetTokenRepository,
        IPasswordResetTokenGenerationService tokenGenerationService,
        ILegacyPasswordService legacyPasswordService,
        IEmailScheduler<PasswordRecoveryEmailPayload> passwordRecoveryEmailScheduler,
        IUserSessionCache userSessionCache,
        IUnitOfWork unitOfWork)
    {
        UserRepository = userRepository;
        PasswordResetTokenRepository = passwordResetTokenRepository;
        TokenGenerationService = tokenGenerationService;
        LegacyPasswordService = legacyPasswordService;
        PasswordRecoveryEmailScheduler = passwordRecoveryEmailScheduler;
        UserSessionCache = userSessionCache;
        UnitOfWork = unitOfWork;
    }

    public IUserRepository UserRepository { get; }
    public IPasswordResetTokenRepository PasswordResetTokenRepository { get; }
    public IPasswordResetTokenGenerationService TokenGenerationService { get; }
    public ILegacyPasswordService LegacyPasswordService { get; }
    public IEmailScheduler<PasswordRecoveryEmailPayload> PasswordRecoveryEmailScheduler { get; }
    public IUserSessionCache UserSessionCache { get; }
    public IUnitOfWork UnitOfWork { get; }
}
