using LgymApi.Api.Features.User.Contracts;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Models;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class UserProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<UserRankingEntry, UserBaseInfoDto>((source, _) => new UserBaseInfoDto
        {
            Name = source.User.Name,
            Avatar = source.User.Avatar,
            Elo = source.Elo,
            ProfileRank = source.User.ProfileRank
        });
    }
}
