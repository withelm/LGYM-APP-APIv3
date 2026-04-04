using AppConfigEntity = LgymApi.Domain.Entities.AppConfig;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.AppConfig;

public interface IAppConfigService
{
    Task<Result<AppConfigEntity, AppError>> GetLatestByPlatformAsync(Platforms platform, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> CreateNewAppVersionAsync(Id<LgymApi.Domain.Entities.User> userId, CreateAppVersionInput input, CancellationToken cancellationToken = default);
}
