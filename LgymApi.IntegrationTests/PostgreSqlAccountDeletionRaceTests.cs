using FluentAssertions;
using LgymApi.Application.Services;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.AccountDeletion;
using LgymApi.Identity.AccountDeletion.Finalization;
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
internal sealed class PostgreSqlAccountDeletionRaceTests : PostgreSqlIntegrationTestBase
{
    [Test]
    public async Task CancellationHoldingAccountLock_WinsAgainstFinalizerThatScannedBeforeCommit()
    {
        var user = await SeedUserAsync();
        var deadline = DateTimeOffset.UtcNow.AddMinutes(1);
        var clock = new RecoveryClock(deadline.AddMinutes(-1));
        string recoveryToken;
        await using (var setup = Factory.Services.CreateAsyncScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<AppDbContext>();
            var account = await db.Users.SingleAsync(candidate => candidate.Id == user.Id);
            account.DeletionState = AccountDeletionState.PendingDeletion;
            account.DeletionRequestedAt = deadline.AddDays(-30);
            account.DeletionEffectiveAt = deadline;
            account.DeletionGeneration = Id<CorrelationScope>.New();
            account.DeletionScheduledGeneration = account.DeletionGeneration;
            await db.SaveChangesAsync();
            var service = new AccountDeletionService(db, new EfUnitOfWork(db), setup.ServiceProvider.GetRequiredService<ILegacyPasswordService>(),
                clock, new AccountDeletionSynchronization(db), setup.ServiceProvider.GetRequiredService<IAccountRecoveryEmailOutbox>(),
                setup.ServiceProvider.GetRequiredService<LgymApi.Application.Platform.Contracts.BackgroundCommands.ICommandOutboxWriter>());
            recoveryToken = await service.SignInWithPasswordAsync(user.Name, "password123");
        }
        var cancellationReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCancellation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finalizerWaiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var cancelScope = Factory.Services.CreateAsyncScope();
        var cancelDb = cancelScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var observed = new ObservedUnitOfWork(new EfUnitOfWork(cancelDb), async () =>
        {
            cancellationReady.SetResult();
            await releaseCancellation.Task.WaitAsync(TimeSpan.FromSeconds(15));
        });
        var cancellation = new AccountDeletionService(cancelDb, observed, cancelScope.ServiceProvider.GetRequiredService<ILegacyPasswordService>(),
            clock, new AccountDeletionSynchronization(cancelDb), cancelScope.ServiceProvider.GetRequiredService<IAccountRecoveryEmailOutbox>(),
            cancelScope.ServiceProvider.GetRequiredService<LgymApi.Application.Platform.Contracts.BackgroundCommands.ICommandOutboxWriter>());
        var cancelTask = cancellation.CancelAsync(recoveryToken);
        try
        {
            await cancellationReady.Task.WaitAsync(TimeSpan.FromSeconds(15));
            clock.Now = deadline;
            await using var finalScope = Factory.Services.CreateAsyncScope();
            var finalDb = finalScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var actualStore = new AccountFinalizationStore(finalDb);
            var store = Substitute.For<IAccountFinalizationStore>();
            store.FindDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(call => actualStore.FindDueAsync(call.Arg<DateTimeOffset>(), call.Arg<int>(), call.Arg<CancellationToken>()));
            store.LockAndBeginAsync(Arg.Any<AccountFinalizationCandidate>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                finalizerWaiting.SetResult();
                return actualStore.LockAndBeginAsync(call.Arg<AccountFinalizationCandidate>(), call.Arg<DateTimeOffset>(), call.Arg<CancellationToken>());
            });
            var finalizer = new AccountFinalizationService(store, finalScope.ServiceProvider.GetRequiredService<IAccountFinalizationPhotoManifest>(),
                new AccountFinalizationCleanup(finalDb), new EfUnitOfWork(finalDb), clock);
            var finalTask = finalizer.ProcessDueAsync();
            await finalizerWaiting.Task.WaitAsync(TimeSpan.FromSeconds(15));
            releaseCancellation.SetResult();
            await Task.WhenAll(cancelTask, finalTask).WaitAsync(TimeSpan.FromSeconds(15));
        }
        finally
        {
            releaseCancellation.TrySetResult();
        }
        await using var verify = Factory.Services.CreateAsyncScope();
        var persisted = await verify.ServiceProvider.GetRequiredService<AppDbContext>().Users.IgnoreQueryFilters().SingleAsync(account => account.Id == user.Id);
        persisted.DeletionState.Should().Be(AccountDeletionState.Active);
        persisted.Name.Should().Be(user.Name);
        persisted.DeletionGeneration.Should().BeNull();
    }

    private sealed class RecoveryClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
