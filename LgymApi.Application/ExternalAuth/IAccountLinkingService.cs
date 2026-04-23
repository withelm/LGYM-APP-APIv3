using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.ExternalAuth;

public interface IAccountLinkingService
{
    Task<Result<Unit, AppError>> LinkGoogleAsync(Id<User> userId, string idToken, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<ExternalLoginInfo>, AppError>> GetExternalLoginsAsync(Id<User> userId, CancellationToken cancellationToken);
}
