using LgymApi.Api.DTOs;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class PlanProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<Plan, PlanFormDto>((source, _) => new PlanFormDto
        {
            Id = source.Id.ToString(),
            Name = source.Name,
            IsActive = source.IsActive
        });

        configuration.CreateMap<Plan, PlanDto>((source, _) => new PlanDto
        {
            Id = source.Id,
            Name = source.Name,
            IsActive = source.IsActive,
            UserId = source.UserId
        });
    }
}
