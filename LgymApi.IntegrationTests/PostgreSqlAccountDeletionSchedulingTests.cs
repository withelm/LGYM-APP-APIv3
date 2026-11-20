using System.Text.Json;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.Platform.Contracts.Serialization;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.BackgroundWorker.Common.Jobs;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.AccountDeletion;
using LgymApi.Identity.AccountDeletion.Finalization;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.AccountDeletion;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories.AccountDeletion;
using LgymApi.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LgymApi.IntegrationTests;

[TestFixture]
[Category("PostgreSql")]
internal sealed class PostgreSqlAccountDeletionSchedulingTests : PostgreSqlIntegrationTestBase
{
    private const string CommandId = "LgymApi.BackgroundWorker.Common.Commands.AccountDeletionScheduledCommand";

    [TestCase(true)]
    [TestCase(false)]
    public async Task Confirmation_RejectsIncompleteSchedulingIdentity(bool missingAccount)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var confirmation = scope.ServiceProvider.GetRequiredService<IAccountDeletionScheduleConfirmation>();
        var command = new AccountDeletionScheduledCommand(
            missingAccount ? Id<AccountReference>.Empty : Id<AccountReference>.New(),
            missingAccount ? Id<CorrelationScope>.New() : Id<CorrelationScope>.Empty);
        await Assert.ThatAsync(async () => { await (((Func<Task>)(() => confirmation.ExecuteAsync(command))))(); }, Throws.InstanceOf<ArgumentException>());
        Assert.That((await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.CountAsync()), Is.EqualTo(0));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Confirmation_MissingOrPermanentlyDeletedAccount_IsTerminalNoOp(bool exists)
    {
        var accountId = Id<AccountReference>.New();
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (exists)
        {
            db.Users.Add(new User { Id = accountId.Rebind<User>(), Name = "deleted", Email = "deleted@example.test", IsDeleted = true, DeletionState = AccountDeletionState.PermanentlyDeleted });
            await db.SaveChangesAsync();
        }
        await scope.ServiceProvider.GetRequiredService<IAccountDeletionScheduleConfirmation>()
            .ExecuteAsync(new AccountDeletionScheduledCommand(accountId, Id<CorrelationScope>.New()));
        Assert.That((await db.Users.IgnoreQueryFilters().CountAsync()), Is.EqualTo(exists ? 1 : 0));
        Assert.That((await db.Users.IgnoreQueryFilters().AnyAsync(account => account.DeletionScheduledGeneration != null)), Is.False);
    }

    [Test]
    public async Task Request_RevokeAndScheduleCommitTogether_AndDuplicatePayloadUsesOneEnvelope()
    {
        var user = await SeedUserAsync();
        var sessionId = await SeedSessionAsync(user.Id);
        await using (var request = Factory.Services.CreateAsyncScope())
        {
            var deletion = request.ServiceProvider.GetRequiredService<IAccountDeletionService>();
            await deletion.RequestAsync(user.Id.Rebind<AccountReference>());
            await deletion.RequestAsync(user.Id.Rebind<AccountReference>());
        }

        await using var verification = Factory.Services.CreateAsyncScope();
        var db = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        var account = await db.Users.SingleAsync(account => account.Id == user.Id);
        Assert.That(account.DeletionState, Is.EqualTo(AccountDeletionState.PendingDeletion));
        Assert.That(account.DeletionScheduledGeneration, Is.Null);
        Assert.That((await db.UserSessions.SingleAsync(session => session.Id == sessionId)).RevokedAtUtc, Is.Not.Null);
        var envelope = await db.CommandEnvelopes.SingleAsync(envelope => envelope.CommandTypeFullName == CommandId);
        var command = JsonSerializer.Deserialize<AccountDeletionScheduledCommand>(envelope.PayloadJson, SharedSerializationOptions.Current)!;
        Assert.That(command.AccountId, Is.EqualTo(user.Id.Rebind<AccountReference>()));
        Assert.That(command.DeletionGeneration, Is.EqualTo(account.DeletionGeneration!.Value));
        var duplicate = await verification.ServiceProvider.GetRequiredService<ICommandOutboxWriter>().StageAsync(command);
        Assert.That(duplicate.WasExisting, Is.True);
        Assert.That(duplicate.EnvelopeId, Is.EqualTo(envelope.Id.ToString()));
        Assert.That((await db.CommandEnvelopes.CountAsync(envelope => envelope.CommandTypeFullName == CommandId)), Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Request_FailureDoesNotCommitPendingStateRevocationOrIntent(bool failAtCommit)
    {
        var user = await SeedUserAsync();
        var sessionId = await SeedSessionAsync(user.Id);
        await using (var request = Factory.Services.CreateAsyncScope())
        {
            var db = request.ServiceProvider.GetRequiredService<AppDbContext>();
            IUnitOfWork unitOfWork = new EfUnitOfWork(db);
            var outbox = request.ServiceProvider.GetRequiredService<ICommandOutboxWriter>();
            if (failAtCommit)
                unitOfWork = new FailingCommitUnitOfWork(unitOfWork);
            else
            {
                outbox = Substitute.For<ICommandOutboxWriter>();
                outbox.StageAsync(Arg.Any<AccountDeletionScheduledCommand>(), Arg.Any<CancellationToken>())
                    .Returns(new CommandEnvelopeStageResult(null, false));
            }
            var deletion = new AccountDeletionService(db, unitOfWork,
                request.ServiceProvider.GetRequiredService<ILegacyPasswordService>(), TimeProvider.System,
                new AccountDeletionSynchronization(db), request.ServiceProvider.GetRequiredService<IAccountRecoveryEmailOutbox>(), outbox);

            await Assert.ThatAsync(async () => { await (((Func<Task>)(() => deletion.RequestAsync(user.Id.Rebind<AccountReference>()))))(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape(failAtCommit ? "injected commit failure" : "Account deletion scheduling intent could not be staged.").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
        }
        await using var verify = Factory.Services.CreateAsyncScope();
        var database = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var account = await database.Users.SingleAsync(account => account.Id == user.Id);
        Assert.That(account.DeletionState, Is.EqualTo(AccountDeletionState.Active));
        Assert.That(account.DeletionGeneration, Is.Null);
        Assert.That((await database.UserSessions.SingleAsync(session => session.Id == sessionId)).RevokedAtUtc, Is.Null);
        Assert.That((await database.CommandEnvelopes.CountAsync(envelope => envelope.CommandTypeFullName == CommandId)), Is.EqualTo(0));
    }

    [Test]
    public async Task DeadLetteredSchedule_BlocksDueFinalizationUntilTheIntentIsReplayed()
    {
        var user = await SeedUserAsync();
        Id<CommandEnvelope> envelopeId;
        await using (var request = Factory.Services.CreateAsyncScope())
        {
            await request.ServiceProvider.GetRequiredService<IAccountDeletionService>().RequestAsync(user.Id.Rebind<AccountReference>());
            var db = request.ServiceProvider.GetRequiredService<AppDbContext>();
            var account = await db.Users.SingleAsync(account => account.Id == user.Id);
            account.DeletionEffectiveAt = DateTimeOffset.UtcNow.AddDays(-1);
            var envelope = await db.CommandEnvelopes.SingleAsync(envelope => envelope.CommandTypeFullName == CommandId);
            envelope.Status = ActionExecutionStatus.DeadLettered;
            envelopeId = envelope.Id;
            await db.SaveChangesAsync();
        }
        await using (var before = Factory.Services.CreateAsyncScope())
        {
            await before.ServiceProvider.GetRequiredService<IAccountFinalizationService>().ProcessDueAsync();
            var db = before.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.That((await db.Users.SingleAsync(account => account.Id == user.Id)).DeletionState, Is.EqualTo(AccountDeletionState.PendingDeletion));
            var envelope = await db.CommandEnvelopes.SingleAsync(envelope => envelope.Id == envelopeId);
            envelope.Status = ActionExecutionStatus.Pending;
            await db.SaveChangesAsync();
        }
        await using (var delivery = Factory.Services.CreateAsyncScope())
        {
            var job = delivery.ServiceProvider.GetRequiredService<IActionMessageJob>();
            await job.ExecuteAsync(envelopeId.ToString());
            await job.ExecuteAsync(envelopeId.ToString());
        }
        await using var verify = Factory.Services.CreateAsyncScope();
        var database = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var confirmed = await database.Users.SingleAsync(account => account.Id == user.Id);
        Assert.That(confirmed.DeletionScheduledGeneration, Is.EqualTo(confirmed.DeletionGeneration));
        var revision = confirmed.LifecycleRevision;
        await verify.ServiceProvider.GetRequiredService<IAccountDeletionScheduleConfirmation>()
            .ExecuteAsync(new AccountDeletionScheduledCommand(user.Id.Rebind<AccountReference>(), confirmed.DeletionGeneration!.Value));
        await database.Entry(confirmed).ReloadAsync();
        Assert.That(confirmed.LifecycleRevision, Is.EqualTo(revision));
        Assert.That(((await new AccountFinalizationStore(database).FindDueAsync(DateTimeOffset.UtcNow, 100))).Where(candidate => candidate.AccountId == user.Id.Rebind<AccountReference>()), Has.Exactly(1).Items);
        Assert.That((await database.CommandEnvelopes.SingleAsync(envelope => envelope.Id == envelopeId)).Status, Is.EqualTo(ActionExecutionStatus.Completed));
    }

    [Test]
    public async Task StaleScheduleAfterCancellation_CannotConfirmTheNextDeletionGeneration()
    {
        var user = await SeedUserAsync();
        Id<CommandEnvelope> staleId;
        Id<CommandEnvelope> currentId;
        await using (var request = Factory.Services.CreateAsyncScope())
        {
            var deletion = request.ServiceProvider.GetRequiredService<IAccountDeletionService>();
            await deletion.RequestAsync(user.Id.Rebind<AccountReference>());
            var db = request.ServiceProvider.GetRequiredService<AppDbContext>();
            staleId = (await db.CommandEnvelopes.SingleAsync(envelope => envelope.CommandTypeFullName == CommandId)).Id;
            var token = await deletion.SignInWithPasswordAsync(user.Name, "password123");
            await deletion.CancelAsync(token);
            await deletion.RequestAsync(user.Id.Rebind<AccountReference>());
            currentId = (await db.CommandEnvelopes.SingleAsync(envelope => envelope.CommandTypeFullName == CommandId && envelope.Id != staleId)).Id;
        }
        await using (var stale = Factory.Services.CreateAsyncScope())
            await stale.ServiceProvider.GetRequiredService<IActionMessageJob>().ExecuteAsync(staleId.ToString());
        await using (var verify = Factory.Services.CreateAsyncScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.That((await db.Users.SingleAsync(account => account.Id == user.Id)).DeletionScheduledGeneration, Is.Null);
            Assert.That((await db.CommandEnvelopes.Where(envelope => envelope.CommandTypeFullName == CommandId).Select(envelope => envelope.CorrelationId).Distinct().CountAsync()), Is.EqualTo(2));
        }
        await using (var current = Factory.Services.CreateAsyncScope())
            await current.ServiceProvider.GetRequiredService<IActionMessageJob>().ExecuteAsync(currentId.ToString());
        await using var final = Factory.Services.CreateAsyncScope();
        var restored = await final.ServiceProvider.GetRequiredService<AppDbContext>().Users.SingleAsync(account => account.Id == user.Id);
        Assert.That(restored.DeletionScheduledGeneration, Is.EqualTo(restored.DeletionGeneration));
    }

    private async Task<Id<UserSession>> SeedSessionAsync(Id<User> userId)
    {
        await using var setup = Factory.Services.CreateAsyncScope();
        var db = setup.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = new UserSession { Id = Id<UserSession>.New(), UserId = userId, Jti = Id<UserSession>.New().ToString(), ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(1) };
        db.UserSessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    private sealed class FailingCommitUnitOfWork(IUnitOfWork inner) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => inner.SaveChangesAsync(cancellationToken);
        public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => new FailingCommitTransaction(await inner.BeginTransactionAsync(cancellationToken));
    }

    private sealed class FailingCommitTransaction(IUnitOfWorkTransaction inner) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.FromException(new InvalidOperationException("injected commit failure"));
        public Task RollbackAsync(CancellationToken cancellationToken = default) => inner.RollbackAsync(cancellationToken);
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
