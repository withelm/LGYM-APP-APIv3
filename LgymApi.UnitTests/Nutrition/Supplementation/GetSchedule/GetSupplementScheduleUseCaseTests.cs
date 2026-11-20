using LgymApi.Application.Nutrition.Persistence;
using LgymApi.Application.Nutrition.Supplementation.GetSchedule;
using LgymApi.Application.Nutrition.Supplementation.GetSchedule.Contracts;
using LgymApi.Application.Nutrition.Supplementation.GetSchedule.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using NSubstitute;

namespace LgymApi.UnitTests.Nutrition.Supplementation.GetSchedule;

[TestFixture]
public sealed class GetSupplementScheduleUseCaseTests
{
    private static readonly DateOnly Monday = new(2026, 7, 27);

    [Test]
    public async Task ExecuteAsync_WhenActivePlanExists_ProjectsLegacyOrderAndTakenValues()
    {
        var traineeId = Id<User>.New();
        var plan = CreatePlan(traineeId);
        var later = AddItem(plan, "Later", 2, 1, new TimeSpan(12, 0, 0));
        AddItem(plan, "Earlier", 1, 1, new TimeSpan(8, 0, 0));
        var logs = new List<SupplementIntakeLog>
        {
            new()
            {
                Id = Id<SupplementIntakeLog>.New(),
                TraineeId = traineeId,
                PlanItemId = later.Id,
                IntakeDate = Monday,
                TakenAt = new DateTimeOffset(2026, 7, 27, 12, 5, 0, TimeSpan.Zero)
            }
        };
        var persistence = Substitute.For<ISupplementationPersistence>();
        persistence.GetActivePlanForTraineeAsync(traineeId, CancellationToken.None).Returns(plan);
        persistence.ListIntakeLogsForPlanAsync(traineeId, plan.Id, Monday, Monday, CancellationToken.None).Returns(logs);

        var result = await CreateUseCase(persistence).ExecuteAsync(new(traineeId, Monday));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Select(entry => entry.SupplementName), Is.EqualTo(new[] { "Earlier", "Later" }));
        Assert.That(result.Value.Select(entry => entry.Taken), Is.EqualTo(new[] { false, true }));
        Assert.That(result.Value[1].TakenAt, Is.EqualTo(logs[0].TakenAt));
        Assert.That(result.Value.Select(entry => entry.IntakeDate), Is.All.EqualTo(Monday));
    }

    [Test]
    public async Task ExecuteAsync_WhenNoActivePlan_ReturnsEmptyWithoutReadingLogs()
    {
        var traineeId = Id<User>.New();
        var persistence = Substitute.For<ISupplementationPersistence>();
        persistence.GetActivePlanForTraineeAsync(traineeId, CancellationToken.None).Returns((SupplementPlan?)null);

        var result = await CreateUseCase(persistence).ExecuteAsync(new(traineeId, Monday));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.Empty);
        Assert.That(persistence.ReceivedCalls(), Has.Exactly(1).Items);
    }

    [Test]
    public async Task ExecuteAsync_WhenItemIsUnscheduled_OmitsItFromSchedule()
    {
        var traineeId = Id<User>.New();
        var plan = CreatePlan(traineeId);
        AddItem(plan, "Monday", 1, 1, new TimeSpan(8, 0, 0));
        AddItem(plan, "Tuesday", 2, 2, new TimeSpan(9, 0, 0));
        var persistence = Substitute.For<ISupplementationPersistence>();
        persistence.GetActivePlanForTraineeAsync(traineeId, CancellationToken.None).Returns(plan);
        persistence.ListIntakeLogsForPlanAsync(traineeId, plan.Id, Monday, Monday, CancellationToken.None).Returns([]);

        var result = await CreateUseCase(persistence).ExecuteAsync(new(traineeId, Monday));

        Assert.That(result.Value.Select(entry => entry.SupplementName), Is.EqualTo(new[] { "Monday" }));
    }

    [Test]
    public async Task ExecuteAsync_UsesOnlyTheRequestedSameDayForNoTrackingLogRead()
    {
        var traineeId = Id<User>.New();
        var plan = CreatePlan(traineeId);
        AddItem(plan, "Daily", 1, 1, new TimeSpan(8, 0, 0));
        var persistence = Substitute.For<ISupplementationPersistence>();
        persistence.GetActivePlanForTraineeAsync(traineeId, CancellationToken.None).Returns(plan);
        persistence.ListIntakeLogsForPlanAsync(traineeId, plan.Id, Monday, Monday, CancellationToken.None).Returns([]);

        await CreateUseCase(persistence).ExecuteAsync(new(traineeId, Monday));

        await persistence.Received(1).GetActivePlanForTraineeAsync(traineeId, CancellationToken.None);
        await persistence.Received(1).ListIntakeLogsForPlanAsync(traineeId, plan.Id, Monday, Monday, CancellationToken.None);
        Assert.That(persistence.ReceivedCalls(), Has.Exactly(2).Items);
    }

    [Test]
    public async Task ExecuteAsync_ForwardsCancellationAndDoesNotCallOtherCollaborators()
    {
        var traineeId = Id<User>.New();
        var plan = CreatePlan(traineeId);
        AddItem(plan, "Daily", 1, 1, new TimeSpan(8, 0, 0));
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var persistence = Substitute.For<ISupplementationPersistence>();
        persistence.GetActivePlanForTraineeAsync(traineeId, cancellationToken).Returns(plan);
        persistence.ListIntakeLogsForPlanAsync(traineeId, plan.Id, Monday, Monday, cancellationToken).Returns([]);

        var result = await CreateUseCase(persistence).ExecuteAsync(new(traineeId, Monday), cancellationToken);

        Assert.That(result.IsSuccess, Is.True);
        await persistence.Received(1).GetActivePlanForTraineeAsync(traineeId, cancellationToken);
        await persistence.Received(1).ListIntakeLogsForPlanAsync(traineeId, plan.Id, Monday, Monday, cancellationToken);
        Assert.That(persistence.ReceivedCalls(), Has.Exactly(2).Items);
    }

    private static IGetSupplementScheduleUseCase CreateUseCase(ISupplementationPersistence persistence)
        => new GetSupplementScheduleUseCase(persistence);

    private static SupplementPlan CreatePlan(Id<User> traineeId)
        => new()
        {
            Id = Id<SupplementPlan>.New(),
            TraineeId = traineeId,
            IsActive = true
        };

    private static SupplementPlanItem AddItem(
        SupplementPlan plan,
        string name,
        int order,
        int daysOfWeekMask,
        TimeSpan timeOfDay)
    {
        var item = new SupplementPlanItem
        {
            Id = Id<SupplementPlanItem>.New(),
            PlanId = plan.Id,
            SupplementName = name,
            Dosage = "1",
            Order = order,
            DaysOfWeekMask = (DaysOfWeekSet)daysOfWeekMask,
            TimeOfDay = timeOfDay,
            CreatedAt = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero)
        };
        plan.Items.Add(item);
        return item;
    }
}
