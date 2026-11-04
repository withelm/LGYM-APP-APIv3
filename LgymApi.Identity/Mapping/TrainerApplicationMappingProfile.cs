using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Pagination;
using LgymApi.Domain.Entities;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Approval;

namespace LgymApi.Identity.Mapping;

internal sealed class TrainerApplicationMappingProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<User, TrainerApplication>((source, _) => new TrainerApplication(
            source.Id.Rebind<AccountReference>(), source.Name, source.Email,
            source.TrainerApprovalStatus.GetValueOrDefault(), source.TrainerApplicationCreatedAt.GetValueOrDefault(),
            source.TrainerApprovalReviewedBy?.Rebind<AccountReference>(), source.TrainerApprovalDecidedAt));
        configuration.CreateMap<Pagination<User>, Pagination<TrainerApplication>>((source, context) => new Pagination<TrainerApplication>
        {
            Items = context!.MapList<User, TrainerApplication>(source.Items),
            Page = source.Page, PageSize = source.PageSize, TotalCount = source.TotalCount
        });
    }
}
