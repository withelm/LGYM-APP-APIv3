using LgymApi.Api.Features.User.Contracts;
using LgymApi.Application.Features.User.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Models;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class UserProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<RankInfo, RankDto>((source, _) => new RankDto
        {
            Name = source.Name,
            NeedElo = source.NeedElo
        });

        configuration.CreateMap<UserInfoResult, UserInfoDto>((source, context) => new UserInfoDto
        {
            Name = source.Name,
            Id = source.Id.ToString(),
            Email = source.Email,
            Avatar = source.Avatar,
            Admin = source.Admin,
            ProfileRank = source.ProfileRank,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt,
            Elo = source.Elo,
            NextRank = source.NextRank == null ? null : context!.Map<RankInfo, RankDto>(source.NextRank),
            IsDeleted = source.IsDeleted,
            IsTester = source.IsTester,
            IsVisibleInRanking = source.IsVisibleInRanking
        });

        configuration.CreateMap<LoginResult, LoginResponseDto>((source, context) => new LoginResponseDto
        {
            Token = source.Token,
            User = context!.Map<UserInfoResult, UserInfoDto>(source.User)
        });

        configuration.CreateMap<RankingEntry, UserBaseInfoDto>((source, _) => new UserBaseInfoDto
        {
            Name = source.Name,
            Avatar = source.Avatar,
            Elo = source.Elo,
            ProfileRank = source.ProfileRank
        });

        configuration.CreateMap<int, UserEloDto>((source, _) => new UserEloDto
        {
            Elo = source
        });

        configuration.CreateMap<UserRankingEntry, UserBaseInfoDto>((source, _) => new UserBaseInfoDto
        {
            Name = source.User.Name,
            Avatar = source.User.Avatar,
            Elo = source.Elo,
            ProfileRank = source.User.ProfileRank
        });
    }
}
