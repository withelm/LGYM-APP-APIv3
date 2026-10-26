using FluentAssertions;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Features.Training.Models;
using LgymApi.Application.TrainingPlanning.Contracts.PlanDay;
using LgymApi.Application.WorkoutProgress.Persistence;
using LgymApi.Application.WorkoutProgress.TrainingExecution;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.TrainingPlanning.Contracts;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class TrainingHistoryReadServiceTests
{
    [Test]
    public async Task GetTrainingByDateAsync_UsesPlanningReferenceFactsForHistoryNames()
    {
        var accountId = Id<AccountReference>.New();
        var planDayId = Id<PlanDayReference>.New();
        var training = Training(accountId, planDayId);
        var planDays = Substitute.For<IPlanDayReferenceReadService>();
        planDays.GetByIdsAsync(Arg.Any<IReadOnlyList<Id<PlanDayReference>>>(), CancellationToken.None)
            .Returns([new PlanDayReferenceReadModel(planDayId, Id<PlanReference>.New(), "Push", true, false)]);
        var service = CreateService(accountId, [training], planDays, out _);

        var result = await service.GetTrainingByDateAsync(accountId, UtcTimeRange.ForLegacyUtcDay(training.CreatedAt.UtcDateTime));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].PlanDay.Should().Be(new PlanDayReferenceReadModel(planDayId, result.Value[0].PlanDay!.PlanId, "Push", true, false));
        await planDays.Received(1).GetByIdsAsync(
            Arg.Is<IReadOnlyList<Id<PlanDayReference>>>(ids => ids.SequenceEqual(new[] { planDayId })),
            CancellationToken.None);
    }

    [Test]
    public async Task GetTrainingByDateAsync_RepresentsMissingOrDeletedPlanDayFactsAsNull()
    {
        var accountId = Id<AccountReference>.New();
        var training = Training(accountId, Id<PlanDayReference>.New());
        var planDays = Substitute.For<IPlanDayReferenceReadService>();
        planDays.GetByIdsAsync(Arg.Any<IReadOnlyList<Id<PlanDayReference>>>(), CancellationToken.None).Returns([]);
        var service = CreateService(accountId, [training], planDays, out _);

        var result = await service.GetTrainingByDateAsync(accountId, UtcTimeRange.ForLegacyUtcDay(training.CreatedAt.UtcDateTime));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].PlanDay.Should().BeNull();
    }

    [Test]
    public async Task GetTrainingByDateAsync_ReturnsAllTrainingsWhenTheyShareAPlanDay()
    {
        var accountId = Id<AccountReference>.New();
        var planDayId = Id<PlanDayReference>.New();
        var planId = Id<PlanReference>.New();
        var trainings = new[] { Training(accountId, planDayId), Training(accountId, planDayId) };
        var planDays = Substitute.For<IPlanDayReferenceReadService>();
        planDays.GetByIdsAsync(Arg.Any<IReadOnlyList<Id<PlanDayReference>>>(), CancellationToken.None)
            .Returns(call => call.Arg<IReadOnlyList<Id<PlanDayReference>>>()
                .Select(id => new PlanDayReferenceReadModel(id, planId, "Push", true, false))
                .ToList());
        var service = CreateService(accountId, trainings, planDays, out _);

        var result = await service.GetTrainingByDateAsync(accountId, UtcTimeRange.ForLegacyUtcDay(trainings[0].CreatedAt.UtcDateTime));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(training => training.Id).Should().BeEquivalentTo(trainings.Select(training => training.Id));
        result.Value.Should().OnlyContain(training => training.PlanDay == new PlanDayReferenceReadModel(planDayId, planId, "Push", true, false));
        await planDays.Received(1).GetByIdsAsync(
            Arg.Is<IReadOnlyList<Id<PlanDayReference>>>(ids => ids.SequenceEqual(new[] { planDayId })),
            CancellationToken.None);
    }

    [Test]
    public async Task GetTrainingByDateAsync_UsesWarsawLocalDayHalfOpenRange()
    {
        var accountId = Id<AccountReference>.New();
        var planDayId = Id<PlanDayReference>.New();
        var training = Training(accountId, planDayId);
        var planDays = Substitute.For<IPlanDayReferenceReadService>();
        planDays.GetByIdsAsync(Arg.Any<IReadOnlyList<Id<PlanDayReference>>>(), CancellationToken.None).Returns([]);
        var service = CreateService(accountId, [training], planDays, out var persistence);
        var range = new LocalCalendarDay(new DateOnly(2026, 10, 22), "Europe/Warsaw").ToUtcTimeRange();

        var result = await service.GetTrainingByDateAsync(accountId, range);

        result.IsSuccess.Should().BeTrue();
        await persistence.Received(1).GetByAccountIdAndDateAsync(
            accountId,
            Arg.Is<UtcTimeRange>(value => value.FromUtc == new DateTimeOffset(2026, 10, 21, 22, 0, 0, TimeSpan.Zero)
                && value.ToUtc == new DateTimeOffset(2026, 10, 22, 22, 0, 0, TimeSpan.Zero)),
            CancellationToken.None);
    }

    private static TrainingHistoryReadService CreateService(
        Id<AccountReference> accountId,
        IReadOnlyList<WorkoutTrainingPersistenceModel> trainings,
        IPlanDayReferenceReadService planDays,
        out IWorkoutTrainingPersistence trainingPersistence)
    {
        var accountAccess = Substitute.For<IAccountAccessReader>();
        trainingPersistence = Substitute.For<IWorkoutTrainingPersistence>();
        var scores = Substitute.For<IWorkoutExerciseScorePersistence>();
        accountAccess.GetByIdAsync(accountId, CancellationToken.None)
            .Returns(new AccountAccessFacts(accountId, false, false, [], []));
        trainingPersistence.GetByAccountIdAndDateAsync(accountId, Arg.Any<UtcTimeRange>(), CancellationToken.None)
            .Returns(trainings);
        trainingPersistence.GetExerciseScoreLinksAsync(Arg.Any<IReadOnlyCollection<Id<Training>>>(), CancellationToken.None)
            .Returns([]);
        scores.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Id<ExerciseScore>>>(), CancellationToken.None).Returns([]);
        return new TrainingHistoryReadService(accountAccess, trainingPersistence, scores, planDays);
    }

    private static WorkoutTrainingPersistenceModel Training(Id<AccountReference> accountId, Id<PlanDayReference> planDayId)
        => new(Id<Training>.New(), accountId, planDayId, Id<Gym>.New(), DateTimeOffset.UtcNow, null);
}
