using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.User.Models;
using LgymApi.Domain.Entities;

namespace LgymApi.Application.ExternalAuth;

public interface ILoginResultBuilder
{
    Task<Result<LoginResult, AppError>> BuildAsync(User user, string preferredTimeZone, CancellationToken cancellationToken);
}
