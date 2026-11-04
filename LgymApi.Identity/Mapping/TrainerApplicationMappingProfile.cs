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
        configuration.CreateMap<TrainerApplicationAttempt, TrainerApplication>((source, _) => new TrainerApplication(
            source.ApplicantId.Rebind<AccountReference>(), source.Applicant?.Name ?? string.Empty,
            source.Applicant?.Email.Value ?? string.Empty,
            source.Status, source.CreatedAt, source.ReviewedBy?.Rebind<AccountReference>(), source.DecidedAt, source.Attempt));
        configuration.CreateMap<Pagination<TrainerApplicationAttempt>, Pagination<TrainerApplication>>((source, context) => new Pagination<TrainerApplication>
        {
            Items = context!.MapList<TrainerApplicationAttempt, TrainerApplication>(source.Items),
            Page = source.Page, PageSize = source.PageSize, TotalCount = source.TotalCount
        });
    }
}
