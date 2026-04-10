using AppConfigEntity = LgymApi.Domain.Entities.AppConfig;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;

namespace LgymApi.Application.Features.AppConfig;

public sealed class AppConfigService : IAppConfigService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IAppConfigRepository _appConfigRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AppConfigService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IAppConfigRepository appConfigRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _appConfigRepository = appConfigRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AppConfigEntity, AppError>> GetLatestByPlatformAsync(Platforms platform, CancellationToken cancellationToken = default)
    {
        if (platform == Platforms.Unknown)
        {
            return Result<AppConfigEntity, AppError>.Failure(new InvalidAppConfigError(Messages.FieldRequired));
        }

        AppConfigEntity? config = await _appConfigRepository.GetLatestByPlatformAsync(platform, cancellationToken);
        if (config == null)
        {
            return Result<AppConfigEntity, AppError>.Failure(new AppConfigNotFoundError(Messages.DidntFind));
        }

        return Result<AppConfigEntity, AppError>.Success(config);
    }

    public async Task<Result<Unit, AppError>> CreateNewAppVersionAsync(Id<LgymApi.Domain.Entities.User> userId, CreateAppVersionInput input, CancellationToken cancellationToken = default)
    {
        if (userId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new AppConfigForbiddenError(Messages.Forbidden));
        }

        var user = await _userRepository.FindByIdAsync((Id<LgymApi.Domain.Entities.User>)userId, cancellationToken);
        if (user == null || !await _roleRepository.UserHasPermissionAsync(userId, AuthConstants.Permissions.ManageAppConfig, cancellationToken))
        {
            return Result<Unit, AppError>.Failure(new AppConfigForbiddenError(Messages.Forbidden));
        }

        if (input.Platform == Platforms.Unknown)
        {
            return Result<Unit, AppError>.Failure(new InvalidAppConfigError(Messages.FieldRequired));
        }

        var config = new AppConfigEntity
        {
            Id = Id<AppConfigEntity>.New(),
            Platform = input.Platform,
            MinRequiredVersion = input.MinRequiredVersion ?? string.Empty,
            LatestVersion = input.LatestVersion ?? string.Empty,
            ForceUpdate = input.ForceUpdate,
            UpdateUrl = input.UpdateUrl ?? string.Empty,
            ReleaseNotes = input.ReleaseNotes
        };

        await _appConfigRepository.AddAsync(config, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Unit, AppError>.Success(Unit.Value);
    }
}
