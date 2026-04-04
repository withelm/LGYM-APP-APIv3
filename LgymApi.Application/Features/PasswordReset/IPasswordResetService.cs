using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

using Result = LgymApi.Application.Common.Results.Result<LgymApi.Application.Common.Results.Unit, LgymApi.Application.Common.Errors.AppError>;

namespace LgymApi.Application.Features.PasswordReset;

public interface IPasswordResetService
{
    Task<Result> RequestPasswordResetAsync(string email, string cultureName, CancellationToken cancellationToken);
    Task<Result> ResetPasswordAsync(string plainTextToken, string newPassword, CancellationToken cancellationToken);
}
