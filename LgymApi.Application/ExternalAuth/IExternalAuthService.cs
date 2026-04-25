using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.User.Models;

namespace LgymApi.Application.ExternalAuth;

public interface IExternalAuthService
{
    Task<Result<LoginResult, AppError>> GoogleSignInAsync(string idToken, CancellationToken cancellationToken);
}
