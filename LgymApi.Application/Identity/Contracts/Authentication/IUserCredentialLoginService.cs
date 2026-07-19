using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.User.Models;

namespace LgymApi.Application.Identity.Contracts.Authentication;

public interface IUserCredentialLoginService
{
    Task<Result<LoginResult, AppError>> LoginAsync(string name, string password, CancellationToken cancellationToken = default);
    Task<Result<LoginResult, AppError>> LoginTrainerAsync(string name, string password, CancellationToken cancellationToken = default);
}
