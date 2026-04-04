namespace LgymApi.Application.Features.PasswordReset;

public interface IPasswordResetTokenGenerationService
{
    Task<GeneratedPasswordResetToken> GenerateUniqueAsync(CancellationToken cancellationToken = default);
}
