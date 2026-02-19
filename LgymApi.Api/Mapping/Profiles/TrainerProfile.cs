using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Features.User.Contracts;
using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.Features.User.Models;
using LgymApi.Application.Mapping.Core;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class TrainerProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<RankInfo, RankDto>((source, _) => new RankDto
        {
            Name = source.Name,
            NeedElo = source.NeedElo
        });

        configuration.CreateMap<UserInfoResult, UserInfoDto>((source, _) => new UserInfoDto
        {
            Name = source.Name,
            Id = source.Id.ToString(),
            Email = source.Email,
            Avatar = source.Avatar,
            ProfileRank = source.ProfileRank,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt,
            Elo = source.Elo,
            NextRank = source.NextRank == null
                ? null
                : new RankDto
                {
                    Name = source.NextRank.Name,
                    NeedElo = source.NextRank.NeedElo
                },
            IsDeleted = source.IsDeleted,
            IsVisibleInRanking = source.IsVisibleInRanking,
            Roles = source.Roles,
            PermissionClaims = source.PermissionClaims
        });

        configuration.CreateMap<LoginResult, LoginResponseDto>((source, _) => new LoginResponseDto
        {
            Token = source.Token,
            PermissionClaims = source.PermissionClaims,
            User = source.User == null
                ? null
                : new UserInfoDto
            {
                Name = source.User.Name,
                Id = source.User.Id.ToString(),
                Email = source.User.Email,
                Avatar = source.User.Avatar,
                ProfileRank = source.User.ProfileRank,
                CreatedAt = source.User.CreatedAt,
                UpdatedAt = source.User.UpdatedAt,
                Elo = source.User.Elo,
                NextRank = source.User.NextRank == null
                    ? null
                    : new RankDto
                    {
                        Name = source.User.NextRank.Name,
                        NeedElo = source.User.NextRank.NeedElo
                    },
                IsDeleted = source.User.IsDeleted,
                IsVisibleInRanking = source.User.IsVisibleInRanking,
                Roles = source.User.Roles,
                PermissionClaims = source.User.PermissionClaims
            }
        });

        configuration.CreateMap<TrainerInvitationResult, TrainerInvitationDto>((source, _) => new TrainerInvitationDto
        {
            Id = source.Id.ToString(),
            TrainerId = source.TrainerId.ToString(),
            TraineeId = source.TraineeId.ToString(),
            Code = source.Code,
            Status = source.Status.ToString(),
            ExpiresAt = source.ExpiresAt,
            RespondedAt = source.RespondedAt,
            CreatedAt = source.CreatedAt
        });

        configuration.CreateMap<TrainerDashboardTraineeResult, TrainerDashboardTraineeDto>((source, _) => new TrainerDashboardTraineeDto
        {
            Id = source.Id.ToString(),
            Name = source.Name,
            Email = source.Email,
            Avatar = source.Avatar,
            Status = source.Status,
            IsLinked = source.IsLinked,
            HasPendingInvitation = source.HasPendingInvitation,
            HasExpiredInvitation = source.HasExpiredInvitation,
            LinkedAt = source.LinkedAt,
            LastInvitationExpiresAt = source.LastInvitationExpiresAt,
            LastInvitationRespondedAt = source.LastInvitationRespondedAt
        });
    }
}
