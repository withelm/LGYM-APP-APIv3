using LgymApi.Api.Features.Enum.Contracts;
using LgymApi.Application.Features.Enum.Models;
using LgymApi.Application.Mapping.Core;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class EnumProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<EnumLookupEntry, EnumLookupDto>((source, _) => new EnumLookupDto
        {
            Name = source.Name,
            DisplayName = source.DisplayName
        });

        configuration.CreateMap<EnumLookupResponse, EnumLookupResponseDto>((source, context) => new EnumLookupResponseDto
        {
            EnumType = source.EnumType,
            Values = context!.MapList<EnumLookupEntry, EnumLookupDto>(source.Values)
        });
    }
}
