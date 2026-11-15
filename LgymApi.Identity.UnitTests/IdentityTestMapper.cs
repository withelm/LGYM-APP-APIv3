using System.Reflection;
using LgymApi.Application.Identity.Mapping;
using LgymApi.Application.Mapping.Core;

namespace LgymApi.Identity.UnitTests;

internal static class IdentityTestMapper
{
    internal static IMapper Create() => (IMapper)Activator.CreateInstance(
        typeof(Mapper), BindingFlags.Instance | BindingFlags.NonPublic, null,
        [new IMappingProfile[] { new IdentityAccountContractMappingProfile() }], null)!;
}
