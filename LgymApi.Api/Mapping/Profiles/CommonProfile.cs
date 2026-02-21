using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Application.Mapping.Core;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class CommonProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<string, ResponseMessageDto>((source, _) => new ResponseMessageDto
        {
            Message = source
        });
    }
}
