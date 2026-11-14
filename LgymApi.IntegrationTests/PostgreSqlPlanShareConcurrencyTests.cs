using FluentAssertions;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Repositories;
using LgymApi.Application.TrainingPlanning.Contracts.PlanDay;
using LgymApi.Application.TrainingPlanning.Plan.CopyPlan;
using LgymApi.Application.TrainingPlanning.Plan.GenerateShareCode;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories;
using LgymApi.Infrastructure.UnitOfWork;
using LgymApi.Identity.Contracts;
using LgymApi.TrainingPlanning.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
[Category("PostgreSql")]
internal sealed class PostgreSqlPlanShareConcurrencyTests : PostgreSqlIntegrationTestBase
{
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [TestCase(7, 7, false)]
    [TestCase(1, 30, false)]
    [TestCase(7, 30, true)]
    public async Task ConcurrentIssuance_OnlyCommittedCodeIsReturned(int firstDays, int secondDays, bool validExisting)
    {
        var owner = await SeedUserAsync();
        var plan = new Plan
        {
            Id = Id<Plan>.New(), UserId = owner.Id, Name = "Share race", ShareCode = "LEGACY0001",
            ShareCodeDurationDays = validExisting ? 7 : null,
            ShareCodeExpiresAt = validExisting ? Now.AddDays(7) : null
        };
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Plans.Add(plan);
            await db.SaveChangesAsync();
        }

        var barrier = new SaveBarrier();
        var outcomes = await Task.WhenAll(
            IssueAsync(plan.Id, owner.Id, firstDays, "FIRST00001", barrier),
            IssueAsync(plan.Id, owner.Id, secondDays, "SECOND0001", barrier)).WaitAsync(TimeSpan.FromSeconds(30));

        outcomes.Count(outcome => outcome.Code is not null).Should().Be(1);
        outcomes.Count(outcome => outcome.Conflict).Should().Be(1);
        var winner = outcomes.Single(outcome => outcome.Code is not null);
        await using var verify = Factory.Services.CreateAsyncScope();
        var persisted = await verify.ServiceProvider.GetRequiredService<AppDbContext>().Plans.AsNoTracking().SingleAsync(p => p.Id == plan.Id);
        persisted.ShareCode.Should().Be(winner.Code);
        persisted.ShareCodeDurationDays.Should().Be(winner.Days);
        persisted.ShareCodeExpiresAt.Should().Be(Now.AddDays(winner.Days));
        persisted.ShareCodeRevision.Should().Be(1);
    }

    [Test]
    public async Task Copy_ValidatedBeforeRotation_CanCompleteButOldCodeCannotBeUsedAgain()
    {
        var owner = await SeedUserAsync("race-owner", "race-owner@example.com");
        var recipient = await SeedUserAsync("race-recipient", "race-recipient@example.com");
        var plan = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Source", ShareCode = "OLDCODE001", ShareCodeDurationDays = 7, ShareCodeExpiresAt = Now.AddDays(7) };
        await using (var seed = Factory.Services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Plans.Add(plan);
            await db.SaveChangesAsync();
        }

        await using var copyScope = Factory.Services.CreateAsyncScope();
        var gate = new CopyGate(copyScope.ServiceProvider.GetRequiredService<IPlanExerciseClonePort>());
        var copy = new CopyPlanUseCase(copyScope.ServiceProvider.GetRequiredService<IPlanRepository>(), gate,
            new EfUnitOfWork(copyScope.ServiceProvider.GetRequiredService<AppDbContext>()), new FixedClock());
        var command = new CopyPlanCommand(recipient.Id.Rebind<AccountReference>(), "OLDCODE001");
        var pendingCopy = copy.ExecuteAsync(command);
        try
        {
            await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await using var rotationScope = Factory.Services.CreateAsyncScope();
            var db = rotationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var issue = new GenerateShareCodeUseCase(new PlanRepository(db, _ => "NEWCODE001"), new EfUnitOfWork(db), new FixedClock());
            var rotated = await issue.ExecuteAsync(new GenerateShareCodeCommand(owner.Id.Rebind<AccountReference>(), plan.Id.Rebind<PlanReference>(), 30));
            rotated.IsSuccess.Should().BeTrue();
            rotated.Value.Should().Be("NEWCODE001");
        }
        finally
        {
            gate.Release.TrySetResult();
        }

        (await pendingCopy.WaitAsync(TimeSpan.FromSeconds(20))).IsSuccess.Should().BeTrue();
        await using var verifyScope = Factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await verifyDb.Plans.AsNoTracking().SingleAsync(p => p.Id == plan.Id);
        persisted.ShareCode.Should().Be("NEWCODE001");
        persisted.ShareCodeDurationDays.Should().Be(30);
        persisted.ShareCodeExpiresAt.Should().Be(Now.AddDays(30));
        (await verifyDb.Plans.CountAsync(p => p.UserId == recipient.Id)).Should().Be(1);
        var retry = new CopyPlanUseCase(verifyScope.ServiceProvider.GetRequiredService<IPlanRepository>(),
            verifyScope.ServiceProvider.GetRequiredService<IPlanExerciseClonePort>(), new EfUnitOfWork(verifyDb), new FixedClock());
        (await retry.ExecuteAsync(command)).IsFailure.Should().BeTrue();
    }

    private async Task<Outcome> IssueAsync(Id<Plan> planId, Id<User> ownerId, int days, string candidate, SaveBarrier barrier)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repository = new PlanRepository(db, _ => candidate);
        var useCase = new GenerateShareCodeUseCase(repository, new GatedUnitOfWork(new EfUnitOfWork(db), barrier), new FixedClock());
        var result = await useCase.ExecuteAsync(new GenerateShareCodeCommand(ownerId.Rebind<LgymApi.Identity.Contracts.AccountReference>(), planId.Rebind<LgymApi.TrainingPlanning.Contracts.PlanReference>(), days));
        if (result.IsFailure)
        {
            result.Error.Should().BeOfType<ConflictError>();
            return new Outcome(days, null, true);
        }
        return new Outcome(days, result.Value, false);
    }

    private sealed record Outcome(int Days, string? Code, bool Conflict);

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class CopyGate(IPlanExerciseClonePort inner) : IPlanExerciseClonePort
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<IReadOnlyDictionary<Id<PlanExerciseReference>, Id<PlanExerciseReference>>> StageClonesAsync(
            Id<AccountReference> targetAccountId, IReadOnlyCollection<Id<PlanExerciseReference>> exerciseIds, CancellationToken cancellationToken = default)
        {
            Reached.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            return await inner.StageClonesAsync(targetAccountId, exerciseIds, cancellationToken);
        }
    }

    private sealed class SaveBarrier
    {
        private int _arrivals;
        private readonly TaskCompletionSource _bothArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task WaitAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _arrivals) == 2)
            {
                _bothArrived.TrySetResult();
            }
            await _bothArrived.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
        }
    }

    private sealed class GatedUnitOfWork(IUnitOfWork inner, SaveBarrier barrier) : IUnitOfWork
    {
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await barrier.WaitAsync(cancellationToken);
            return await inner.SaveChangesAsync(cancellationToken);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => inner.BeginTransactionAsync(cancellationToken);
    }
}
