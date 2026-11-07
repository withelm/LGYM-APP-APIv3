using FluentAssertions;
using LgymApi.Application.WorkoutProgress.Authorization;
using LgymApi.Application.WorkoutProgress;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class WorkoutProgressOwnershipAuthorizationTests
{
    [Test]
    public void RegisteredPolicy_AllowsTheMatchingNonEmptyOwner()
    {
        var ownerId = Id<AccountReference>.New();
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IWorkoutProgressOwnershipAuthorizationService>();

        service.IsOwner(ownerId, ownerId).Should().BeTrue();
    }

    [Test]
    public void RegisteredPolicy_DeniesForeignAndEmptyActors()
    {
        var ownerId = Id<AccountReference>.New();
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IWorkoutProgressOwnershipAuthorizationService>();

        service.IsOwner(Id<AccountReference>.New(), ownerId).Should().BeFalse();
        service.IsOwner(Id<AccountReference>.Empty, ownerId).Should().BeFalse();
        service.IsOwner(ownerId, Id<AccountReference>.Empty).Should().BeFalse();
    }

    private static ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddWorkoutAndProgressModule()
            .BuildServiceProvider(validateScopes: true);
}
