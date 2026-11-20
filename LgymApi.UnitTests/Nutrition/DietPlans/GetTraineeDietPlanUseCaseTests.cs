using LgymApi.Application.Coaching.Contracts.Access;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Coaching.Errors;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Nutrition.DietPlans.GetTraineePlan;
using LgymApi.Application.Nutrition.DietPlans.Models;
using LgymApi.Application.Nutrition.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests.Nutrition.DietPlans;

[TestFixture]
public sealed class GetTraineeDietPlanUseCaseTests
{
    [Test]
    public async Task ExecuteAsync_WhenPlanIsOwned_MapsTheNoTrackingPlan()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var plan = CreatePlan(trainerId, traineeId);
        var mappedPlan = CreateReadModel(plan);
        using var cancellationSource = new CancellationTokenSource();
        var dependencies = new Dependencies();
        dependencies.Access.GetAccessDecisionAsync(trainerId, traineeId, cancellationSource.Token)
            .Returns(new CoachingRelationshipAccessDecision(true, true));
        dependencies.Plans.GetPlanByIdAsync(plan.Id, cancellationSource.Token)
            .Returns(plan);
        dependencies.Mapper.Map<DietPlan, DietPlanReadModel>(plan, Arg.Any<MappingContext?>())
            .Returns(mappedPlan);

        var result = await dependencies.CreateUseCase().ExecuteAsync(
            new GetTraineeDietPlanQuery(NutritionAuthorizationTestFactory.CreateActor(trainerId), trainerId, traineeId, plan.Id),
            cancellationSource.Token);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.SameAs(mappedPlan));
        await dependencies.Access.Received(1).GetAccessDecisionAsync(trainerId, traineeId, cancellationSource.Token);
        await dependencies.Plans.Received(1).GetPlanByIdAsync(plan.Id, cancellationSource.Token);
        await dependencies.Plans.DidNotReceive().FindTrackedPlanByIdAsync(
            Arg.Any<Id<DietPlan>>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_WhenTraineeIdIsEmpty_ReturnsBadRequestBeforeCoaching()
    {
        var dependencies = new Dependencies();

        var result = await dependencies.CreateUseCase().ExecuteAsync(
            new GetTraineeDietPlanQuery(NutritionAuthorizationTestFactory.CreateActor(Id<User>.New()), Id<User>.New(), Id<User>.Empty, Id<DietPlan>.New()));

        Assert.That(result.Error, Is.TypeOf<BadRequestError>());
        Assert.That(result.Error.Message, Is.EqualTo(Messages.UserIdRequired));
        await dependencies.Access.DidNotReceiveWithAnyArgs().GetAccessDecisionAsync(default, default, default);
        await AssertNoPlanLoadsOrWritesAsync(dependencies);
        Assert.That(dependencies.Mapper.ReceivedCalls(), Is.Empty);
    }

    [Test]
    public async Task ExecuteAsync_WhenCallerIsNotTrainer_ReturnsForbiddenBeforePlanLookup()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var dependencies = new Dependencies();
        dependencies.Access.GetAccessDecisionAsync(trainerId, traineeId, Arg.Any<CancellationToken>())
            .Returns(new CoachingRelationshipAccessDecision(false, true));

        var result = await dependencies.CreateUseCase().ExecuteAsync(
            new GetTraineeDietPlanQuery(NutritionAuthorizationTestFactory.CreateActor(trainerId), trainerId, traineeId, Id<DietPlan>.New()));

        Assert.That(result.Error, Is.TypeOf<TrainerRelationshipForbiddenError>());
        Assert.That(result.Error.Message, Is.EqualTo(Messages.TrainerRoleRequired));
        await AssertNoPlanLoadsOrWritesAsync(dependencies);
        Assert.That(dependencies.Mapper.ReceivedCalls(), Is.Empty);
    }

    [Test]
    public async Task ExecuteAsync_WhenNoActiveLinkExists_ReturnsNotFoundBeforePlanLookup()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var dependencies = new Dependencies();
        dependencies.Access.GetAccessDecisionAsync(trainerId, traineeId, Arg.Any<CancellationToken>())
            .Returns(new CoachingRelationshipAccessDecision(true, false));

        var result = await dependencies.CreateUseCase().ExecuteAsync(
            new GetTraineeDietPlanQuery(NutritionAuthorizationTestFactory.CreateActor(trainerId), trainerId, traineeId, Id<DietPlan>.New()));

        Assert.That(result.Error, Is.TypeOf<NotFoundError>());
        Assert.That(result.Error.Message, Is.EqualTo(Messages.DidntFind));
        await AssertNoPlanLoadsOrWritesAsync(dependencies);
        Assert.That(dependencies.Mapper.ReceivedCalls(), Is.Empty);
    }

    [Test]
    public async Task ExecuteAsync_WhenDietPlanIdIsEmptyAfterAccess_ReturnsBadRequestWithoutPlanLookup()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var dependencies = new Dependencies();
        dependencies.Access.GetAccessDecisionAsync(trainerId, traineeId, Arg.Any<CancellationToken>())
            .Returns(new CoachingRelationshipAccessDecision(true, true));

        var result = await dependencies.CreateUseCase().ExecuteAsync(
            new GetTraineeDietPlanQuery(NutritionAuthorizationTestFactory.CreateActor(trainerId), trainerId, traineeId, Id<DietPlan>.Empty));

        Assert.That(result.Error, Is.TypeOf<BadRequestError>());
        Assert.That(result.Error.Message, Is.EqualTo(Messages.FieldRequired));
        await dependencies.Access.Received(1).GetAccessDecisionAsync(
            trainerId,
            traineeId,
            Arg.Any<CancellationToken>());
        await AssertNoPlanLoadsOrWritesAsync(dependencies);
        Assert.That(dependencies.Mapper.ReceivedCalls(), Is.Empty);
    }

    [TestCase(OwnershipFailure.Missing)]
    [TestCase(OwnershipFailure.WrongTrainee)]
    [TestCase(OwnershipFailure.Deleted)]
    public async Task ExecuteAsync_WhenPlanIsMissingOrNotOwned_ReturnsNotFoundWithoutMapping(
        OwnershipFailure ownershipFailure)
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var planId = Id<DietPlan>.New();
        var dependencies = new Dependencies();
        dependencies.Access.GetAccessDecisionAsync(trainerId, traineeId, Arg.Any<CancellationToken>())
            .Returns(new CoachingRelationshipAccessDecision(true, true));
        dependencies.Plans.GetPlanByIdAsync(planId, Arg.Any<CancellationToken>())
            .Returns(CreateOwnedPlanFailure(ownershipFailure, planId, trainerId, traineeId));

        var result = await dependencies.CreateUseCase().ExecuteAsync(
            new GetTraineeDietPlanQuery(NutritionAuthorizationTestFactory.CreateActor(trainerId), trainerId, traineeId, planId));

        Assert.That(result.Error, Is.TypeOf<NotFoundError>());
        Assert.That(result.Error.Message, Is.EqualTo(Messages.DidntFind));
        await dependencies.Plans.Received(1).GetPlanByIdAsync(planId, Arg.Any<CancellationToken>());
        await AssertNoTrackedLoadOrWritesAsync(dependencies);
        Assert.That(dependencies.Mapper.ReceivedCalls(), Is.Empty);
    }

    private static async Task AssertNoPlanLoadsOrWritesAsync(Dependencies dependencies)
    {
        await dependencies.Plans.DidNotReceiveWithAnyArgs().GetPlanByIdAsync(default, default);
        await AssertNoTrackedLoadOrWritesAsync(dependencies);
    }

    private static async Task AssertNoTrackedLoadOrWritesAsync(Dependencies dependencies)
    {
        await dependencies.Plans.DidNotReceiveWithAnyArgs().FindTrackedPlanByIdAsync(default, default);
        await dependencies.Plans.DidNotReceiveWithAnyArgs().AddPlanAsync(default!, default);
        await dependencies.Plans.DidNotReceiveWithAnyArgs().AddHistoryEntryAsync(default!, default);
    }

    private static DietPlan? CreateOwnedPlanFailure(
        OwnershipFailure ownershipFailure,
        Id<DietPlan> planId,
        Id<User> trainerId,
        Id<User> traineeId)
        => ownershipFailure switch
        {
            OwnershipFailure.Missing => null,
            OwnershipFailure.ForeignTrainer => CreatePlan(Id<User>.New(), traineeId, planId),
            OwnershipFailure.WrongTrainee => CreatePlan(trainerId, Id<User>.New(), planId),
            OwnershipFailure.Deleted => CreatePlan(trainerId, traineeId, planId, isDeleted: true),
            _ => throw new ArgumentOutOfRangeException(nameof(ownershipFailure), ownershipFailure, null)
        };

    private static DietPlan CreatePlan(
        Id<User> trainerId,
        Id<User> traineeId,
        Id<DietPlan>? planId = null,
        bool isDeleted = false)
        => new()
        {
            Id = planId ?? Id<DietPlan>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId,
            Name = "Nutrition plan",
            StartDate = new DateOnly(2026, 7, 23),
            IsDeleted = isDeleted
        };

    private static DietPlanReadModel CreateReadModel(DietPlan plan)
        => new(
            plan.Id,
            plan.TrainerId,
            plan.TraineeId,
            plan.Name,
            plan.StartDate,
            plan.EndDate,
            plan.EstimatedCalories,
            plan.ProteinGrams,
            plan.CarbsGrams,
            plan.FatGrams,
            plan.Notes,
            plan.IsActive,
            plan.CreatedAt,
            plan.UpdatedAt,
            []);

    public enum OwnershipFailure
    {
        Missing,
        ForeignTrainer,
        WrongTrainee,
        Deleted
    }

    private sealed class Dependencies
    {
        public ICoachingRelationshipAccessService Access { get; } = Substitute.For<ICoachingRelationshipAccessService>();
        public IDietPlanPersistence Plans { get; } = Substitute.For<IDietPlanPersistence>();
        public IMapper Mapper { get; } = Substitute.For<IMapper>();

        public IGetTraineeDietPlanUseCase CreateUseCase()
            => new GetTraineeDietPlanUseCase(Access, Plans, Mapper, NutritionAuthorizationTestFactory.CreateDietCoordinator());
    }
}
