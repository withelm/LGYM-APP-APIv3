using AppConfigEntity = LgymApi.Domain.Entities.AppConfig;
using LgymApi.Application.Exceptions;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Enums;
using LgymApi.Resources;

namespace LgymApi.Application.Features.AppConfig;

public sealed class AppConfigService : IAppConfigService
{
    private readonly IUserRepository _userRepository;
    private readonly IAppConfigRepository _appConfigRepository;

    public AppConfigService(IUserRepository userRepository, IAppConfigRepository appConfigRepository)
    {
        _userRepository = userRepository;
        _appConfigRepository = appConfigRepository;
    }

    public async Task<AppConfigEntity> GetLatestByPlatformAsync(Platforms platform)
    {
        if (platform == Platforms.Unknown)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        AppConfigEntity? config = await _appConfigRepository.GetLatestByPlatformAsync(platform);
        if (config == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return config;
    }

    public async Task CreateNewAppVersionAsync(
        Guid userId,
        Platforms platform,
        string? minRequiredVersion,
        string? latestVersion,
        bool forceUpdate,
        string? updateUrl,
        string? releaseNotes)
    {
        if (userId == Guid.Empty)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        var user = await _userRepository.FindByIdAsync(userId);
        if (user == null || user.Admin != true)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        if (platform == Platforms.Unknown)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var config = new AppConfigEntity
        {
            Id = Guid.NewGuid(),
            Platform = platform,
            MinRequiredVersion = minRequiredVersion,
            LatestVersion = latestVersion,
            ForceUpdate = forceUpdate,
            UpdateUrl = updateUrl,
            ReleaseNotes = releaseNotes
        };

        await _appConfigRepository.AddAsync((global::LgymApi.Domain.Entities.AppConfig)config);
    }
}
