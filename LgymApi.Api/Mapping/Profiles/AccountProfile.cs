using LgymApi.Api.Features.Account.Contracts;
using LgymApi.Application.ExternalAuth;
using LgymApi.Application.Mapping.Core;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class AccountProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<ExternalLoginInfo, ExternalLoginDto>((source, _) => new ExternalLoginDto
        {
            Provider = source.Provider,
            ProviderEmail = source.ProviderEmail
        });
    }
}
