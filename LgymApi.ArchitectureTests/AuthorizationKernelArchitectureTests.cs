using LgymApi.Authorization;
using LgymApi.Authorization.Contracts;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class AuthorizationKernelArchitectureTests
{
    [Test]
    public void Authorization_Kernel_ShouldExposeOnlyTypedPolicyContracts()
    {
        var publicTypes = typeof(AuthorizationPolicyEvaluator<>).Assembly
            .GetExportedTypes()
            .Select(type => type.FullName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.That(publicTypes, Is.EqualTo(new[]
        {
            typeof(AuthorizationActor).FullName,
            typeof(AuthorizationDecision).FullName,
            typeof(IAuthorizationEvaluator<>).FullName,
            typeof(IAuthorizationPolicy<>).FullName,
            typeof(AuthorizationPolicyEvaluator<>).FullName,
            typeof(LgymApi.Authorization.AuthorizationServiceCollectionExtensions).FullName
        }.OrderBy(name => name, StringComparer.Ordinal)));
    }
}
