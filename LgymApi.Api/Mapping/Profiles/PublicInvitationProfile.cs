using LgymApi.Api.Features.Public.Contracts;
using LgymApi.Application.Mapping.Core;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class PublicInvitationProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<(string Status, bool UserExists), PublicInvitationStatusDto>((source, _) => new PublicInvitationStatusDto
        {
            Status = source.Status,
            UserExists = source.UserExists
        });
    }
}
