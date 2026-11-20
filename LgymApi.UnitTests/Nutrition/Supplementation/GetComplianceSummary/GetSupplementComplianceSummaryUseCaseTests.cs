using LgymApi.Application.Coaching.Contracts.Access;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Nutrition.Errors;
using LgymApi.Application.Nutrition.Persistence;
using LgymApi.Application.Nutrition.Supplementation.GetComplianceSummary;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using NSubstitute;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.UnitTests.Nutrition.Supplementation.GetComplianceSummary;

[TestFixture]
public sealed class GetSupplementComplianceSummaryUseCaseTests
{
    [Test]
    public async Task ExecuteAsync_WhenActivePlanHasLogs_UsesInclusiveScheduleAndRoundsToTwoDecimals()
    {
        var trainerId = Id<UserEntity>.New();
        var traineeId = Id<UserEntity>.New();
        var plan = CreatePlan(trainerId, traineeId);
        plan.Items.Add(CreateItem(plan, 127, 1));
        plan.Items.Add(CreateItem(plan, 1, 2));
        var dependencies = new Dependencies();
        dependencies.Access.GetAccessDecisionAsync(trainerId, traineeId, CancellationToken.None)
            .Returns(new CoachingRelationshipAccessDecision(true, true));
        dependencies.Plans.GetActivePlanForTraineeAsync(traineeId, CancellationToken.None).Returns(plan);
        dependencies.Plans.ListIntakeLogsForPlanAsync(
                traineeId,
                plan.Id,
                new DateOnly(2026, 7, 6),
                new DateOnly(2026, 7, 7),
                CancellationToken.None)
            .Returns([new SupplementIntakeLog { TraineeId = traineeId, PlanItemId = plan.Items.First().Id, IntakeDate = new DateOnly(2026, 7, 6) }]);

        var result = await dependencies.CreateUseCase().ExecuteAsync(
            new GetSupplementComplianceSummaryQuery(NutritionAuthorizationTestFactory.CreateActor(trainerId), trainerId, traineeId, new DateOnly(2026, 7, 6), new DateOnly(2026, 7, 7)));

        Assert.That(result.Value.PlannedDoses, Is.EqualTo(3));
        Assert.That(result.Value.TakenDoses, Is.EqualTo(1));
        Assert.That(result.Value.AdherenceRate, Is.EqualTo(33.33));
        Assert.That(result.Value.FromDate, Is.EqualTo(new DateOnly(2026, 7, 6)));
        Assert.That(result.Value.ToDate, Is.EqualTo(new DateOnly(2026, 7, 7)));
    }

    [Test]
    public async Task ExecuteAsync_WhenPlanHasNoScheduledItems_ReturnsZeroSummary()
    {
        var trainerId = Id<UserEntity>.New();
        var traineeId = Id<UserEntity>.New();
        var plan = CreatePlan(trainerId, traineeId);
        plan.Items.Add(CreateItem(plan, 2, 1));
        var dependencies = new Dependencies();
        dependencies.Access.GetAccessDecisionAsync(trainerId, traineeId, CancellationToken.None)
            .Returns(new CoachingRelationshipAccessDecision(true, true));
        dependencies.Plans.GetActivePlanForTraineeAsync(traineeId, CancellationToken.None).Returns(plan);
        dependencies.Plans.ListIntakeLogsForPlanAsync(
                Arg.Any<Id<UserEntity>>(),
                Arg.Any<Id<SupplementPlan>>(),
                Arg.Any<DateOnly>(),
                Arg.Any<DateOnly>(),
                Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await dependencies.CreateUseCase().ExecuteAsync(
            new GetSupplementComplianceSummaryQuery(NutritionAuthorizationTestFactory.CreateActor(trainerId), trainerId, traineeId, new DateOnly(2026, 7, 6), new DateOnly(2026, 7, 6)));

        Assert.That(result.Value.PlannedDoses, Is.EqualTo(0));
        Assert.That(result.Value.TakenDoses, Is.EqualTo(0));
        Assert.That(result.Value.AdherenceRate, Is.EqualTo(0));
    }

    [Test]
    public async Task ExecuteAsync_WhenNoActivePlan_ReturnsZeroSummaryWithoutLogsRead()
    {
        var trainerId = Id<UserEntity>.New();
        var traineeId = Id<UserEntity>.New();
        var dependencies = LinkedDependencies(trainerId, traineeId);
        dependencies.Plans.GetActivePlanForTraineeAsync(traineeId, CancellationToken.None).Returns((SupplementPlan?)null);

        var result = await dependencies.CreateUseCase().ExecuteAsync(
            new GetSupplementComplianceSummaryQuery(NutritionAuthorizationTestFactory.CreateActor(trainerId), trainerId, traineeId, new DateOnly(2026, 7, 6), new DateOnly(2026, 7, 7)));

        Assert.That(result.Value.PlannedDoses, Is.EqualTo(0));
        Assert.That(result.Value.TakenDoses, Is.EqualTo(0));
        Assert.That(result.Value.AdherenceRate, Is.EqualTo(0));
        Assert.That(dependencies.Plans.ReceivedCalls(), Has.Exactly(1).Items);
    }

    [Test]
    public async Task ExecuteAsync_WhenActivePlanBelongsToAnotherTrainer_ReturnsZeroSummaryWithoutLogsRead()
    {
        var trainerId = Id<UserEntity>.New();
        var traineeId = Id<UserEntity>.New();
        var plan = CreatePlan(Id<UserEntity>.New(), traineeId);
        var dependencies = LinkedDependencies(trainerId, traineeId);
        dependencies.Plans.GetActivePlanForTraineeAsync(traineeId, CancellationToken.None).Returns(plan);

        var result = await dependencies.CreateUseCase().ExecuteAsync(
            new GetSupplementComplianceSummaryQuery(NutritionAuthorizationTestFactory.CreateActor(trainerId), trainerId, traineeId, new DateOnly(2026, 7, 6), new DateOnly(2026, 7, 7)));

        Assert.That(result.Value.PlannedDoses, Is.EqualTo(0));
        Assert.That(result.Value.TakenDoses, Is.EqualTo(0));
        Assert.That(dependencies.Plans.ReceivedCalls(), Has.Exactly(1).Items);
    }

    [Test]
    public async Task ExecuteAsync_WhenDateRangeIsReversedOrTooLarge_ReturnsTheMatchingDateError()
    {
        var trainerId = Id<UserEntity>.New();
        var traineeId = Id<UserEntity>.New();
        var dependencies = LinkedDependencies(trainerId, traineeId);
        var useCase = dependencies.CreateUseCase();

        var maximum = await useCase.ExecuteAsync(
            new GetSupplementComplianceSummaryQuery(NutritionAuthorizationTestFactory.CreateActor(trainerId), trainerId, traineeId, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1)));
        var reversed = await useCase.ExecuteAsync(
            new GetSupplementComplianceSummaryQuery(NutritionAuthorizationTestFactory.CreateActor(trainerId), trainerId, traineeId, new DateOnly(2026, 7, 2), new DateOnly(2026, 7, 1)));
        var oversized = await useCase.ExecuteAsync(
            new GetSupplementComplianceSummaryQuery(NutritionAuthorizationTestFactory.CreateActor(trainerId), trainerId, traineeId, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 2)));

        Assert.That(maximum.IsSuccess, Is.True);
        Assert.That(reversed.Error, Is.TypeOf<InvalidSupplementationError>());
        Assert.That(reversed.Error.Message, Is.EqualTo(Messages.InvalidDateRange));
        Assert.That(oversized.Error, Is.TypeOf<InvalidSupplementationError>());
        Assert.That(oversized.Error.Message, Is.EqualTo(Messages.DateRangeTooLarge));
        Assert.That(dependencies.Plans.ReceivedCalls(), Has.Exactly(1).Items);
    }

    [Test]
    public async Task ExecuteAsync_WhenRequesterIsNotTrainer_ReturnsForbiddenBeforeDateValidation()
    {
        var trainerId = Id<UserEntity>.New();
        var dependencies = new Dependencies();
        dependencies.Access.GetAccessDecisionAsync(trainerId, Id<UserEntity>.Empty, CancellationToken.None)
            .Returns(new CoachingRelationshipAccessDecision(false, true));

        var result = await dependencies.CreateUseCase().ExecuteAsync(
            new GetSupplementComplianceSummaryQuery(NutritionAuthorizationTestFactory.CreateActor(trainerId), trainerId, Id<UserEntity>.Empty, new DateOnly(2026, 7, 2), new DateOnly(2026, 7, 1)));

        Assert.That(result.Error, Is.TypeOf<SupplementationForbiddenError>());
        Assert.That(result.Error.Message, Is.EqualTo(Messages.TrainerRoleRequired));
        Assert.That(dependencies.Plans.ReceivedCalls(), Is.Empty);
    }

    [Test]
    public async Task ExecuteAsync_ForwardsCancellationToAccessAndNoTrackingReads()
    {
        var trainerId = Id<UserEntity>.New();
        var traineeId = Id<UserEntity>.New();
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var plan = CreatePlan(trainerId, traineeId);
        var dependencies = new Dependencies();
        dependencies.Access.GetAccessDecisionAsync(trainerId, traineeId, cancellationToken)
            .Returns(new CoachingRelationshipAccessDecision(true, true));
        dependencies.Plans.GetActivePlanForTraineeAsync(traineeId, cancellationToken).Returns(plan);
        dependencies.Plans.ListIntakeLogsForPlanAsync(
                traineeId, plan.Id, new DateOnly(2026, 7, 6), new DateOnly(2026, 7, 7), cancellationToken)
            .Returns([]);

        var result = await dependencies.CreateUseCase().ExecuteAsync(
            new GetSupplementComplianceSummaryQuery(NutritionAuthorizationTestFactory.CreateActor(trainerId), trainerId, traineeId, new DateOnly(2026, 7, 6), new DateOnly(2026, 7, 7)),
            cancellationToken);

        Assert.That(result.IsSuccess, Is.True);
        await dependencies.Access.Received(1).GetAccessDecisionAsync(trainerId, traineeId, cancellationToken);
        await dependencies.Plans.Received(1).GetActivePlanForTraineeAsync(traineeId, cancellationToken);
        await dependencies.Plans.Received(1).ListIntakeLogsForPlanAsync(
            traineeId, plan.Id, new DateOnly(2026, 7, 6), new DateOnly(2026, 7, 7), cancellationToken);
    }

    private static Dependencies LinkedDependencies(Id<UserEntity> trainerId, Id<UserEntity> traineeId)
    {
        var dependencies = new Dependencies();
        dependencies.Access.GetAccessDecisionAsync(trainerId, traineeId, CancellationToken.None)
            .Returns(new CoachingRelationshipAccessDecision(true, true));
        return dependencies;
    }

    private static SupplementPlan CreatePlan(Id<UserEntity> trainerId, Id<UserEntity> traineeId)
        => new() { Id = Id<SupplementPlan>.New(), TrainerId = trainerId, TraineeId = traineeId, IsActive = true };

    private static SupplementPlanItem CreateItem(SupplementPlan plan, int daysOfWeekMask, int order)
        => new()
        {
            Id = Id<SupplementPlanItem>.New(),
            PlanId = plan.Id,
            DaysOfWeekMask = (DaysOfWeekSet)daysOfWeekMask,
            Order = order
        };

    private sealed class Dependencies
    {
        public ICoachingRelationshipAccessService Access { get; } = Substitute.For<ICoachingRelationshipAccessService>();
        public ISupplementationPersistence Plans { get; } = Substitute.For<ISupplementationPersistence>();

        public IGetSupplementComplianceSummaryUseCase CreateUseCase()
            => new GetSupplementComplianceSummaryUseCase(Access, Plans, NutritionAuthorizationTestFactory.CreateSupplementationCoordinator());
    }
}
