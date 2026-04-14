using LgymApi.Api.Features.AppConfig.Contracts;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class AppConfigProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<AppConfig, AppConfigInfoDto>((source, _) => new AppConfigInfoDto
        {
            MinRequiredVersion = source.MinRequiredVersion,
            LatestVersion = source.LatestVersion,
            ForceUpdate = source.ForceUpdate,
            UpdateUrl = source.UpdateUrl,
            ReleaseNotes = source.ReleaseNotes
        });

        configuration.CreateMap<AppConfig, AppConfigDetailDto>((source, _) => new AppConfigDetailDto
        {
            Id = source.Id.ToString(),
            Platform = source.Platform,
            MinRequiredVersion = source.MinRequiredVersion,
            LatestVersion = source.LatestVersion,
            ForceUpdate = source.ForceUpdate,
            UpdateUrl = source.UpdateUrl,
            ReleaseNotes = source.ReleaseNotes,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt
        });
    }
}
