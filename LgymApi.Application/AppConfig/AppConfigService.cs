using AppConfigEntity = LgymApi.Domain.Entities.AppConfig;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Pagination;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;

namespace LgymApi.Application.AppConfig;

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

    public async Task<Result<Pagination<AppConfigEntity>, AppError>> GetPaginatedAsync(Id<LgymApi.Domain.Entities.User> userId, FilterInput filterInput, CancellationToken cancellationToken = default)
    {
        if (userId.IsEmpty)
        {
            return Result<Pagination<AppConfigEntity>, AppError>.Failure(new AppConfigForbiddenError(Messages.Forbidden));
        }

        var user = await _userRepository.FindByIdAsync(userId, cancellationToken);
        if (user == null || !await _roleRepository.UserHasPermissionAsync(userId, AuthConstants.Permissions.ManageAppConfig, cancellationToken))
        {
            return Result<Pagination<AppConfigEntity>, AppError>.Failure(new AppConfigForbiddenError(Messages.Forbidden));
        }

        var pagination = await _appConfigRepository.GetPaginatedAsync(filterInput, cancellationToken);
        return Result<Pagination<AppConfigEntity>, AppError>.Success(pagination);
    }

    public async Task<Result<AppConfigEntity, AppError>> GetByIdAsync(Id<LgymApi.Domain.Entities.User> userId, Id<AppConfigEntity> configId, CancellationToken cancellationToken = default)
    {
        if (userId.IsEmpty)
        {
            return Result<AppConfigEntity, AppError>.Failure(new AppConfigForbiddenError(Messages.Forbidden));
        }

        var user = await _userRepository.FindByIdAsync(userId, cancellationToken);
        if (user == null || !await _roleRepository.UserHasPermissionAsync(userId, AuthConstants.Permissions.ManageAppConfig, cancellationToken))
        {
            return Result<AppConfigEntity, AppError>.Failure(new AppConfigForbiddenError(Messages.Forbidden));
        }

        if (configId.IsEmpty)
        {
            return Result<AppConfigEntity, AppError>.Failure(new InvalidAppConfigError(Messages.FieldRequired));
        }

        var config = await _appConfigRepository.FindByIdAsync(configId, cancellationToken);
        if (config == null)
        {
            return Result<AppConfigEntity, AppError>.Failure(new AppConfigNotFoundError(Messages.DidntFind));
        }

        return Result<AppConfigEntity, AppError>.Success(config);
    }

    public async Task<Result<Unit, AppError>> UpdateAsync(Id<LgymApi.Domain.Entities.User> userId, Id<AppConfigEntity> configId, UpdateAppConfigInput input, CancellationToken cancellationToken = default)
    {
        if (userId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new AppConfigForbiddenError(Messages.Forbidden));
        }

        var user = await _userRepository.FindByIdAsync(userId, cancellationToken);
        if (user == null || !await _roleRepository.UserHasPermissionAsync(userId, AuthConstants.Permissions.ManageAppConfig, cancellationToken))
        {
            return Result<Unit, AppError>.Failure(new AppConfigForbiddenError(Messages.Forbidden));
        }

        if (configId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidAppConfigError(Messages.FieldRequired));
        }

        if (input.Platform == Platforms.Unknown)
        {
            return Result<Unit, AppError>.Failure(new InvalidAppConfigError(Messages.FieldRequired));
        }

        var config = await _appConfigRepository.FindByIdTrackedAsync(configId, cancellationToken);
        if (config == null)
        {
            return Result<Unit, AppError>.Failure(new AppConfigNotFoundError(Messages.DidntFind));
        }

        config.Platform = input.Platform;
        config.MinRequiredVersion = input.MinRequiredVersion ?? string.Empty;
        config.LatestVersion = input.LatestVersion ?? string.Empty;
        config.ForceUpdate = input.ForceUpdate;
        config.UpdateUrl = input.UpdateUrl ?? string.Empty;
        config.ReleaseNotes = input.ReleaseNotes;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> DeleteAsync(Id<LgymApi.Domain.Entities.User> userId, Id<AppConfigEntity> configId, CancellationToken cancellationToken = default)
    {
        if (userId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new AppConfigForbiddenError(Messages.Forbidden));
        }

        var user = await _userRepository.FindByIdAsync(userId, cancellationToken);
        if (user == null || !await _roleRepository.UserHasPermissionAsync(userId, AuthConstants.Permissions.ManageAppConfig, cancellationToken))
        {
            return Result<Unit, AppError>.Failure(new AppConfigForbiddenError(Messages.Forbidden));
        }

        if (configId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidAppConfigError(Messages.FieldRequired));
        }

        var config = await _appConfigRepository.FindByIdTrackedAsync(configId, cancellationToken);
        if (config == null)
        {
            return Result<Unit, AppError>.Failure(new AppConfigNotFoundError(Messages.DidntFind));
        }

        _appConfigRepository.Delete(config);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Unit, AppError>.Success(Unit.Value);
    }
}
