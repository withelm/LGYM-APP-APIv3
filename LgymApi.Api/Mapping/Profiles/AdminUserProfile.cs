using LgymApi.Api.Features.AdminManagement.Contracts;
using LgymApi.Application.Features.AdminManagement.Models;
using LgymApi.Application.Identity.ApiAdapters;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Pagination;
using LgymApi.Identity.Contracts.Approval;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class AdminUserProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<TrainerApplication, TrainerApplicationDto>((source, _) => new TrainerApplicationDto
        {
            AccountId = source.AccountId.ToString(), Name = source.Name, Email = source.Email,
            Status = source.Status, CreatedAt = source.CreatedAt, Attempt = source.Attempt,
            ReviewedBy = source.ReviewedBy?.ToString(), DecidedAt = source.DecidedAt
        });
        configuration.CreateMap<Pagination<TrainerApplication>, PaginatedTrainerApplicationsDto>((source, context) => new PaginatedTrainerApplicationsDto
        {
            Items = context!.MapList<TrainerApplication, TrainerApplicationDto>(source.Items),
            Page = source.Page, PageSize = source.PageSize, TotalCount = source.TotalCount,
            TotalPages = source.TotalPages
        });
        configuration.CreateMap<UserResult, AdminUserDto>((source, _) => new AdminUserDto
        {
            Id = source.Id.ToString(),
            Name = source.Name,
            Email = source.Email,
            Avatar = source.Avatar,
            ProfileRank = source.ProfileRank,
            IsVisibleInRanking = source.IsVisibleInRanking,
            IsBlocked = source.IsBlocked,
            IsDeleted = source.IsDeleted,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt,
            Roles = source.Roles
        });

        configuration.CreateMap<AdminAccountProjection, AdminUserDto>((source, _) => new AdminUserDto
        {
            Id = source.Id.ToString(),
            Name = source.Name,
            Email = source.Email,
            Avatar = source.Avatar,
            ProfileRank = source.ProfileRank,
            IsVisibleInRanking = source.IsVisibleInRanking,
            IsBlocked = source.IsBlocked,
            IsDeleted = source.IsDeleted,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt,
            Roles = source.Roles.ToList()
        });
    }
}
