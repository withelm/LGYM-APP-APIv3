using LgymApi.Api.Features.EloRegistry.Contracts;
using LgymApi.Application.Features.EloRegistry.Models;
using LgymApi.Application.Mapping.Core;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class EloRegistryProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<EloRegistryChartEntry, EloRegistryBaseChartDto>((source, _) => new EloRegistryBaseChartDto
        {
            Id = source.Id,
            Value = source.Value,
            Date = source.Date
        });
    }
}
