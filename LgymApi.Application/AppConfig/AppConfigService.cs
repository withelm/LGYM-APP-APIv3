using AppConfigEntity = LgymApi.Domain.Entities.AppConfig;
using LgymApi.Application.Exceptions;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
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

    public async Task<AppConfigEntity> GetLatestByPlatformAsync(Platforms platform, CancellationToken cancellationToken = default)
    {
        if (platform == Platforms.Unknown)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        AppConfigEntity? config = await _appConfigRepository.GetLatestByPlatformAsync(platform, cancellationToken);
        if (config == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return config;
    }

    public async Task CreateNewAppVersionAsync(Guid userId, CreateAppVersionInput input, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        var user = await _userRepository.FindByIdAsync(userId, cancellationToken);
        if (user == null || !await _roleRepository.UserHasPermissionAsync(userId, AuthConstants.Permissions.ManageAppConfig, cancellationToken))
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        if (input.Platform == Platforms.Unknown)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var config = new AppConfigEntity
        {
            Id = Guid.NewGuid(),
            Platform = input.Platform,
            MinRequiredVersion = input.MinRequiredVersion ?? string.Empty,
            LatestVersion = input.LatestVersion ?? string.Empty,
            ForceUpdate = input.ForceUpdate,
            UpdateUrl = input.UpdateUrl ?? string.Empty,
            ReleaseNotes = input.ReleaseNotes
        };

        await _appConfigRepository.AddAsync(config, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
