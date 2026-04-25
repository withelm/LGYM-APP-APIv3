using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Services;
using LgymApi.Domain.Entities;

namespace LgymApi.Application.ExternalAuth;

public interface IGoogleUserRegistrar
{
    Task<Result<User, AppError>> RegisterAsync(GoogleTokenPayload payload, CancellationToken cancellationToken);
}
