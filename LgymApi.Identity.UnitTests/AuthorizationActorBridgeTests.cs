using FluentAssertions;
using LgymApi.Application.Identity.Authorization;
using LgymApi.Application.Mapping.Core;
using LgymApi.Authorization;
using LgymApi.Authorization.Contracts;
using LgymApi.Domain.ValueObjects;
using LgymApi.Domain.Security;
using LgymApi.Identity;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Identity.Contracts.Authorization;
using LgymApi.Platform.Contracts;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LgymApi.Identity.UnitTests;

[TestFixture]
public sealed class AuthorizationActorBridgeTests
{
    [Test]
    public void Evaluate_MapsTheAuthenticatedAccountToTheAuthorizationActorBeforeEvaluating()
    {
        var accountContext = CreateAccountContext(["permissions.manage"]);
        var actor = new AuthorizationActor(accountContext.Id.Rebind<ActorReference>());
        var mapper = Substitute.For<IMapper>();
        var evaluator = Substitute.For<IAuthorizationEvaluator<GlobalPermissionAuthorizationContext>>();
        mapper.Map<AuthenticatedAccountContext, AuthorizationActor>(accountContext, null).Returns(actor);
        evaluator.Evaluate(actor, Arg.Any<GlobalPermissionAuthorizationContext>()).Returns(AuthorizationDecision.Allow);
        var service = new AuthenticatedAccountAuthorizationService(mapper, evaluator);

        var result = service.HasPermission(accountContext, "permissions.manage");

        result.Should().BeTrue();
        mapper.Received(1).Map<AuthenticatedAccountContext, AuthorizationActor>(accountContext, null);
        evaluator.Received(1).Evaluate(
            actor,
            Arg.Is<GlobalPermissionAuthorizationContext>(context =>
                context.Permission == "permissions.manage"
                && context.PermissionClaims.SequenceEqual(accountContext.PermissionClaims)));
    }

    [Test]
    public void CreateActor_UsesTheRegisteredIdentityMappingBridge()
    {
        var accountContext = CreateAccountContext([]);
        var expectedActor = new AuthorizationActor(accountContext.Id.Rebind<ActorReference>());
        var mapper = Substitute.For<IMapper>();
        mapper.Map<AuthenticatedAccountContext, AuthorizationActor>(accountContext, null).Returns(expectedActor);
        var service = new AuthenticatedAccountAuthorizationService(
            mapper,
            Substitute.For<IAuthorizationEvaluator<GlobalPermissionAuthorizationContext>>());

        service.CreateActor(accountContext).Should().BeSameAs(expectedActor);
        mapper.Received(1).Map<AuthenticatedAccountContext, AuthorizationActor>(accountContext, null);
    }

    [Test]
    public void Evaluate_UsesTheCurrentPersistedPermissionFactsWithoutRoleBypass()
    {
        var accountContext = CreateAccountContext([], ["Admin", "Trainer"]);
        var actor = new AuthorizationActor(accountContext.Id.Rebind<ActorReference>());
        var mapper = Substitute.For<IMapper>();
        mapper.Map<AuthenticatedAccountContext, AuthorizationActor>(accountContext, null).Returns(actor);
        var evaluator = new AuthorizationPolicyEvaluator<GlobalPermissionAuthorizationContext>([new GlobalPermissionAuthorizationPolicy()]);
        var service = new AuthenticatedAccountAuthorizationService(mapper, evaluator);

        var result = service.HasPermission(accountContext, "permissions.manage");

        result.Should().BeFalse();
    }

    [Test]
    public void Evaluate_AllowsManageGlobalExercisesOnlyWithItsExplicitPermissionClaim()
    {
        var accountContext = CreateAccountContext([AuthConstants.Permissions.ManageGlobalExercises]);
        var actor = new AuthorizationActor(accountContext.Id.Rebind<ActorReference>());
        var mapper = Substitute.For<IMapper>();
        mapper.Map<AuthenticatedAccountContext, AuthorizationActor>(accountContext, null).Returns(actor);
        var evaluator = new AuthorizationPolicyEvaluator<GlobalPermissionAuthorizationContext>([new GlobalPermissionAuthorizationPolicy()]);
        var service = new AuthenticatedAccountAuthorizationService(mapper, evaluator);

        service.HasPermission(accountContext, AuthConstants.Permissions.ManageGlobalExercises).Should().BeTrue();
        service.HasPermission(accountContext, "permissions.other").Should().BeFalse();
    }

    [Test]
    public void GlobalPermissionPolicy_DeniesWhenNoPolicyIsRegistered()
    {
        var evaluator = new AuthorizationPolicyEvaluator<GlobalPermissionAuthorizationContext>([]);

        var result = evaluator.Evaluate(
            new AuthorizationActor(Id<ActorReference>.New()),
            new GlobalPermissionAuthorizationContext("permissions.manage", ["permissions.manage"]));

        result.Should().Be(AuthorizationDecision.Deny);
    }

    [Test]
    public void AddIdentityModule_RegistersExactlyOneGlobalPermissionPolicyAndBridge()
    {
        var services = new ServiceCollection();

        services.AddIdentityModule();

        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IAuthorizationPolicy<GlobalPermissionAuthorizationContext>)
            && descriptor.ImplementationType == typeof(GlobalPermissionAuthorizationPolicy));
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IAuthenticatedAccountAuthorizationService)
            && descriptor.ImplementationType == typeof(AuthenticatedAccountAuthorizationService));
    }

    [Test]
    public void AddIdentityModule_RejectsDuplicateGlobalPermissionPolicyRegistration()
    {
        var services = new ServiceCollection();
        services.AddIdentityModule();

        var act = () => services.AddIdentityModule();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*GlobalPermissionAuthorizationContext*");
    }

    private static AuthenticatedAccountContext CreateAccountContext(
        IReadOnlyList<string> permissionClaims,
        IReadOnlyList<string>? roles = null) =>
        new(
            Id<AccountReference>.New(),
            Id<AccountSessionReference>.New(),
            roles ?? [],
            permissionClaims,
            IsBlocked: false,
            IsDeleted: false);
}
