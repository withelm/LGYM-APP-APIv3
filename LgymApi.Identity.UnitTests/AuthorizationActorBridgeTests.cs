using LgymApi.Application.Identity.Authorization;
using LgymApi.Application.Mapping.Core;
using LgymApi.Authorization;
using LgymApi.Authorization.Contracts;
using LgymApi.Domain.Enums;
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

        Assert.That(result, Is.True);
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

        Assert.That(service.CreateActor(accountContext), Is.SameAs(expectedActor));
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

        Assert.That(result, Is.False);
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

        Assert.That(service.HasPermission(accountContext, AuthConstants.Permissions.ManageGlobalExercises), Is.True);
        Assert.That(service.HasPermission(accountContext, "permissions.other"), Is.False);
    }

    [TestCase(AccountDeletionState.PendingDeletion)]
    [TestCase(AccountDeletionState.Finalizing)]
    [TestCase(AccountDeletionState.PermanentlyDeleted)]
    [TestCase((AccountDeletionState)999)]
    public void Evaluate_DeniesPrivilegedClaimsForNonActiveLifecycle(AccountDeletionState state)
    {
        var account = CreateAccountContext([AuthConstants.Permissions.AdminAccess, AuthConstants.Permissions.TrainerAccess], ["Admin", "Trainer"]) with { DeletionState = state };
        var service = new AuthenticatedAccountAuthorizationService(
            IdentityTestMapper.Create(),
            new AuthorizationPolicyEvaluator<GlobalPermissionAuthorizationContext>([new GlobalPermissionAuthorizationPolicy()]));

        Assert.That(service.HasPermission(account, AuthConstants.Permissions.AdminAccess), Is.False);
        Assert.That(service.HasPermission(account, AuthConstants.Permissions.TrainerAccess), Is.False);
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public void Evaluate_DeniesPrivilegedClaimsForBlockedOrDeletedAccount(bool blocked, bool deleted)
    {
        var account = CreateAccountContext([AuthConstants.Permissions.AdminAccess]) with { IsBlocked = blocked, IsDeleted = deleted };
        var service = new AuthenticatedAccountAuthorizationService(
            IdentityTestMapper.Create(),
            new AuthorizationPolicyEvaluator<GlobalPermissionAuthorizationContext>([new GlobalPermissionAuthorizationPolicy()]));

        Assert.That(service.HasPermission(account, AuthConstants.Permissions.AdminAccess), Is.False);
    }

    [Test]
    public void Evaluate_DeniesWhenLifecycleIsUnknown()
    {
        var account = CreateAccountContext([AuthConstants.Permissions.AdminAccess]) with { DeletionState = null };
        var service = new AuthenticatedAccountAuthorizationService(
            IdentityTestMapper.Create(),
            new AuthorizationPolicyEvaluator<GlobalPermissionAuthorizationContext>([new GlobalPermissionAuthorizationPolicy()]));

        Assert.That(service.HasPermission(account, AuthConstants.Permissions.AdminAccess), Is.False);
    }

    [Test]
    public void GlobalPermissionPolicy_DeniesWhenNoPolicyIsRegistered()
    {
        var evaluator = new AuthorizationPolicyEvaluator<GlobalPermissionAuthorizationContext>([]);

        var result = evaluator.Evaluate(
            new AuthorizationActor(Id<ActorReference>.New()),
            new GlobalPermissionAuthorizationContext("permissions.manage", ["permissions.manage"]));

        Assert.That(result, Is.EqualTo(AuthorizationDecision.Deny));
    }

    [Test]
    public void AddIdentityModule_RegistersExactlyOneGlobalPermissionPolicyAndBridge()
    {
        var services = new ServiceCollection();

        services.AddIdentityModule();

        Assert.That((services).Where(descriptor =>
            descriptor.ServiceType == typeof(IAuthorizationPolicy<GlobalPermissionAuthorizationContext>)
            && descriptor.ImplementationType == typeof(GlobalPermissionAuthorizationPolicy)), Has.Exactly(1).Items);
        Assert.That((services).Where(descriptor =>
            descriptor.ServiceType == typeof(IAuthenticatedAccountAuthorizationService)
            && descriptor.ImplementationType == typeof(AuthenticatedAccountAuthorizationService)), Has.Exactly(1).Items);
    }

    [Test]
    public void AddIdentityModule_RejectsDuplicateGlobalPermissionPolicyRegistration()
    {
        var services = new ServiceCollection();
        services.AddIdentityModule();

        var act = () => services.AddIdentityModule();

        Assert.That(() => { act(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*GlobalPermissionAuthorizationContext*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
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
            IsDeleted: false,
            DeletionState: AccountDeletionState.Active);
}
