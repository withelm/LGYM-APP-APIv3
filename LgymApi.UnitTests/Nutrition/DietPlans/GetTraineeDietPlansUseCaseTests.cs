using LgymApi.Application.Coaching.Contracts.Access;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Coaching.Errors;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Nutrition.DietPlans.GetTraineePlans;
using LgymApi.Application.Nutrition.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.UnitTests.Nutrition.DietPlans;

[TestFixture]
public sealed class GetTraineeDietPlansUseCaseTests
{
    [Test]
    public async Task ExecuteAsync_WhenTrainerIsLinked_MapsTheNoTrackingPlansInLegacyOrder()
    {
        var trainerId = Id<UserEntity>.New();
        var traineeId = Id<UserEntity>.New();
        var plans = new List<DietPlan>
        {
            CreatePlan(trainerId, traineeId, "Active latest start", true, new DateOnly(2026, 7, 3), new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero)),
            CreatePlan(trainerId, traineeId, "Active same start newer created", true, new DateOnly(2026, 7, 2), new DateTimeOffset(2026, 7, 2, 8, 0, 0, TimeSpan.Zero)),
            CreatePlan(trainerId, traineeId, "Active same start older created", true, new DateOnly(2026, 7, 2), new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero)),
            CreatePlan(trainerId, traineeId, "Inactive", false, new DateOnly(2026, 7, 4), new DateTimeOffset(2026, 7, 4, 8, 0, 0, TimeSpan.Zero))
        };
        var dependencies = new Dependencies();
        dependencies.Access.GetAccessDecisionAsync(trainerId, traineeId, CancellationToken.None)
            .Returns(new CoachingRelationshipAccessDecision(true, true));
        dependencies.Plans.ListPlansByTraineeAsync(traineeId, CancellationToken.None)
            .Returns(plans);

        var result = await dependencies.CreateUseCase().ExecuteAsync(new GetTraineeDietPlansQuery(NutritionAuthorizationTestFactory.CreateActor(trainerId), trainerId, traineeId));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Select(plan => plan.Name), Is.EqualTo(plans.Select(plan => plan.Name)));
        Assert.That(result.Value.Select(plan => plan.IsActive), Is.EqualTo(new[] { true, true, true, false }));
        Assert.That(result.Value.Select(plan => plan.StartDate), Is.EqualTo(new[] { new DateOnly(2026, 7, 3), new DateOnly(2026, 7, 2), new DateOnly(2026, 7, 2), new DateOnly(2026, 7, 4) }));
        Assert.That(result.Value.Select(plan => plan.CreatedAt), Is.EqualTo(plans.Select(plan => plan.CreatedAt)));
        await dependencies.Access.Received(1).GetAccessDecisionAsync(trainerId, traineeId, CancellationToken.None);
        await dependencies.Plans.Received(1).ListPlansByTraineeAsync(traineeId, CancellationToken.None);
        Assert.That(dependencies.Plans.ReceivedCalls(), Has.Exactly(1).Items);
    }

    [Test]
    public async Task ExecuteAsync_WhenTraineeIdIsEmpty_ReturnsBadRequestWithoutCoachingAccess()
    {
        var trainerId = Id<UserEntity>.New();
        var dependencies = new Dependencies();

        var result = await dependencies.CreateUseCase().ExecuteAsync(
            new GetTraineeDietPlansQuery(NutritionAuthorizationTestFactory.CreateActor(trainerId), trainerId, Id<UserEntity>.Empty));

        Assert.That(result.Error, Is.TypeOf<BadRequestError>());
        Assert.That(result.Error.Message, Is.EqualTo(Messages.UserIdRequired));
        await dependencies.Access.DidNotReceive().GetAccessDecisionAsync(
            Arg.Any<Id<UserEntity>>(),
            Arg.Any<Id<UserEntity>>(),
            Arg.Any<CancellationToken>());
        Assert.That(dependencies.Plans.ReceivedCalls(), Is.Empty);
    }

    [Test]
    public async Task ExecuteAsync_WhenRequesterIsNotTrainer_ReturnsForbiddenBeforePersistence()
    {
        var trainerId = Id<UserEntity>.New();
        var traineeId = Id<UserEntity>.New();
        var dependencies = new Dependencies();
        dependencies.Access.GetAccessDecisionAsync(trainerId, traineeId, CancellationToken.None)
            .Returns(new CoachingRelationshipAccessDecision(false, false));

        var result = await dependencies.CreateUseCase().ExecuteAsync(new GetTraineeDietPlansQuery(NutritionAuthorizationTestFactory.CreateActor(trainerId), trainerId, traineeId));

        Assert.That(result.Error, Is.TypeOf<TrainerRelationshipForbiddenError>());
        Assert.That(result.Error.Message, Is.EqualTo(Messages.TrainerRoleRequired));
        Assert.That(result.Error.HttpStatusCode, Is.EqualTo(403));
        await dependencies.Access.Received(1).GetAccessDecisionAsync(trainerId, traineeId, CancellationToken.None);
        Assert.That(dependencies.Plans.ReceivedCalls(), Is.Empty);
    }

    [Test]
    public async Task ExecuteAsync_WhenTrainerHasNoActiveLink_ReturnsNotFoundBeforePersistence()
    {
        var trainerId = Id<UserEntity>.New();
        var traineeId = Id<UserEntity>.New();
        var dependencies = new Dependencies();
        dependencies.Access.GetAccessDecisionAsync(trainerId, traineeId, CancellationToken.None)
            .Returns(new CoachingRelationshipAccessDecision(true, false));

        var result = await dependencies.CreateUseCase().ExecuteAsync(new GetTraineeDietPlansQuery(NutritionAuthorizationTestFactory.CreateActor(trainerId), trainerId, traineeId));

        Assert.That(result.Error, Is.TypeOf<NotFoundError>());
        Assert.That(result.Error.Message, Is.EqualTo(Messages.DidntFind));
        await dependencies.Access.Received(1).GetAccessDecisionAsync(trainerId, traineeId, CancellationToken.None);
        Assert.That(dependencies.Plans.ReceivedCalls(), Is.Empty);
    }

    [Test]
    public async Task ExecuteAsync_ForwardsCancellationToAccessAndNoTrackingPersistence()
    {
        var trainerId = Id<UserEntity>.New();
        var traineeId = Id<UserEntity>.New();
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var dependencies = new Dependencies();
        dependencies.Access.GetAccessDecisionAsync(trainerId, traineeId, cancellationToken)
            .Returns(new CoachingRelationshipAccessDecision(true, true));
        dependencies.Plans.ListPlansByTraineeAsync(traineeId, cancellationToken)
            .Returns([]);

        var result = await dependencies.CreateUseCase().ExecuteAsync(
            new GetTraineeDietPlansQuery(NutritionAuthorizationTestFactory.CreateActor(trainerId), trainerId, traineeId),
            cancellationToken);

        Assert.That(result.IsSuccess, Is.True);
        await dependencies.Access.Received(1).GetAccessDecisionAsync(trainerId, traineeId, cancellationToken);
        await dependencies.Plans.Received(1).ListPlansByTraineeAsync(traineeId, cancellationToken);
        Assert.That(dependencies.Plans.ReceivedCalls(), Has.Exactly(1).Items);
    }

    private static DietPlan CreatePlan(
        Id<UserEntity> trainerId,
        Id<UserEntity> traineeId,
        string name,
        bool isActive,
        DateOnly startDate,
        DateTimeOffset createdAt)
        => new()
        {
            Id = Id<DietPlan>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId,
            Name = name,
            StartDate = startDate,
            IsActive = isActive,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

    private sealed class Dependencies
    {
        public ICoachingRelationshipAccessService Access { get; } = Substitute.For<ICoachingRelationshipAccessService>();
        public IDietPlanPersistence Plans { get; } = Substitute.For<IDietPlanPersistence>();
        public IMapper Mapper { get; } = CreateMapper();

        public IGetTraineeDietPlansUseCase CreateUseCase()
            => new GetTraineeDietPlansUseCase(Access, Plans, Mapper, NutritionAuthorizationTestFactory.CreateDietCoordinator());

        private static IMapper CreateMapper()
        {
            var services = new ServiceCollection();
            services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);

            using var provider = services.BuildServiceProvider();
            return provider.GetRequiredService<IMapper>();
        }
    }
}
