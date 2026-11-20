using LgymApi.Application.Identity.Access;
using LgymApi.Application.Identity.Authorization;
using LgymApi.Application.Identity.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Repositories;
using LgymApi.Authorization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Approval;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Identity.Contracts.Authorization;
using NSubstitute;

namespace LgymApi.Identity.UnitTests;

[TestFixture]
public sealed class AccountLifecycleAuthorizationTests
{
    [TestCase(AccountDeletionState.Active)]
    [TestCase(AccountDeletionState.PendingDeletion)]
    [TestCase(AccountDeletionState.Finalizing)]
    [TestCase(AccountDeletionState.PermanentlyDeleted)]
    public void Mapping_PreservesPersistedLifecycleIntoFactsAndContext(AccountDeletionState state)
    {
        var mapper = IdentityTestMapper.Create();
        var user = new User { Id = Id<User>.New(), DeletionState = state };

        var facts = mapper.Map<User, AccountAccessFacts>(user);
        var context = mapper.Map<AccountAccessFacts, AuthenticatedAccountContext>(facts);

        Assert.That(facts.DeletionState, Is.EqualTo(state));
        Assert.That(context.DeletionState, Is.EqualTo(state));
    }

    [TestCase(AccountDeletionState.PendingDeletion)]
    [TestCase(AccountDeletionState.Finalizing)]
    [TestCase(AccountDeletionState.PermanentlyDeleted)]
    public async Task ReaderAndResolver_HideNonActiveAccounts(AccountDeletionState state)
    {
        var user = new User { Id = Id<User>.New(), DeletionState = state };
        var mapper = IdentityTestMapper.Create();
        var reader = new AccountAccessReader(new LifecycleUserRepository(user), new LifecycleRoleRepository(), mapper, null!);
        var sessions = Substitute.For<IAccountSessionValidator>();
        var accountId = user.Id.Rebind<AccountReference>();
        var sessionId = Id<AccountSessionReference>.New();
        sessions.IsValidAsync(accountId, sessionId, Arg.Any<CancellationToken>()).Returns(true);
        var resolver = new AuthenticatedAccountContextResolver(sessions, reader, mapper);

        var facts = await reader.GetByIdAsync(accountId);
        var resolution = await resolver.ResolveAsync(accountId, sessionId);

        Assert.That(facts, Is.Null);
        Assert.That(resolution.Status, Is.EqualTo(AuthenticatedAccountResolutionStatus.AccountNotFound));
        Assert.That(resolution.Context, Is.Null);
    }

    [Test]
    public async Task RestoredAccount_ReevaluatesCurrentFactsForSameIdentity()
    {
        var user = new User { Id = Id<User>.New(), DeletionState = AccountDeletionState.Active };
        var accountId = user.Id.Rebind<AccountReference>();
        var sessionId = Id<AccountSessionReference>.New();
        var mapper = IdentityTestMapper.Create();
        var reader = new AccountAccessReader(new LifecycleUserRepository(user), new LifecycleRoleRepository(), mapper, null!);
        var sessions = Substitute.For<IAccountSessionValidator>();
        sessions.IsValidAsync(accountId, sessionId, Arg.Any<CancellationToken>()).Returns(true);
        var resolver = new AuthenticatedAccountContextResolver(sessions, reader, mapper);
        var authorization = new AuthenticatedAccountAuthorizationService(mapper,
            new AuthorizationPolicyEvaluator<GlobalPermissionAuthorizationContext>([new GlobalPermissionAuthorizationPolicy()]));

        var initial = await resolver.ResolveAsync(accountId, sessionId);
        user.DeletionState = AccountDeletionState.PendingDeletion;
        var pending = await resolver.ResolveAsync(accountId, sessionId);
        user.DeletionState = AccountDeletionState.Active;
        var restored = await resolver.ResolveAsync(accountId, sessionId);

        Assert.That(initial.Status, Is.EqualTo(AuthenticatedAccountResolutionStatus.Active));
        Assert.That(authorization.HasPermission(initial.Context!, AuthConstants.Permissions.AdminAccess), Is.True);
        Assert.That(pending.Context, Is.Null);
        Assert.That(restored.Status, Is.EqualTo(AuthenticatedAccountResolutionStatus.Active));
        Assert.That(restored.Context!.Id, Is.EqualTo(accountId));
        Assert.That(restored.Context.DeletionState, Is.EqualTo(AccountDeletionState.Active));
        Assert.That(authorization.HasPermission(restored.Context, AuthConstants.Permissions.AdminAccess), Is.True);
    }

    [TestCase(AccountDeletionState.PendingDeletion)]
    [TestCase(AccountDeletionState.Finalizing)]
    [TestCase(AccountDeletionState.PermanentlyDeleted)]
    public async Task Resolver_RejectsNonActiveFactsFromReader(AccountDeletionState state)
    {
        var accountId = Id<AccountReference>.New();
        var sessionId = Id<AccountSessionReference>.New();
        var sessions = Substitute.For<IAccountSessionValidator>();
        sessions.IsValidAsync(accountId, sessionId, Arg.Any<CancellationToken>()).Returns(true);
        var reader = Substitute.For<IAccountAccessReader>();
        reader.GetByIdAsync(accountId, Arg.Any<CancellationToken>())
            .Returns(new AccountAccessFacts(accountId, false, false, ["Admin"], [AuthConstants.Permissions.AdminAccess], state));
        var resolver = new AuthenticatedAccountContextResolver(sessions, reader, IdentityTestMapper.Create());

        var result = await resolver.ResolveAsync(accountId, sessionId);

        Assert.That(result.Status, Is.EqualTo(AuthenticatedAccountResolutionStatus.AccountNotFound));
        Assert.That(result.Context, Is.Null);
    }
}
