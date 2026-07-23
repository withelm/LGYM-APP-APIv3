using FluentAssertions;
using LgymApi.Application.Coaching.Contracts.Access;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Features.DietPlans;
using LgymApi.Application.Features.DietPlans.Models;
using LgymApi.Application.Nutrition.Contracts.BackgroundCommands;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class DietPlanServiceTests
{
    [Test]
    public async Task CreateTraineePlanAsync_WhenLinked_StagesHistoryCommitsThenNotifies()
    {
        var trainer = User(Id<User>.New());
        var traineeId = Id<User>.New();
        var operations = new List<string>();
        var dependencies = new Dependencies();
        DietPlan? stagedPlan = null;
        DietPlanHistory? stagedHistory = null;
        DietPlanUpdatedInAppNotificationCommand? notification = null;
        dependencies.Access.GetAccessDecisionAsync(trainer.Id, traineeId, Arg.Any<CancellationToken>())
            .Returns(new CoachingRelationshipAccessDecision(true, true))
            .AndDoes(_ => operations.Add("access"));
        dependencies.Plans.AddPlanAsync(Arg.Any<DietPlan>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(call =>
            {
                stagedPlan = call.Arg<DietPlan>();
                operations.Add("plan");
            });
        dependencies.Plans.AddHistoryEntryAsync(Arg.Any<DietPlanHistory>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(call =>
            {
                stagedHistory = call.Arg<DietPlanHistory>();
                operations.Add("history");
            });
        dependencies.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(2)
            .AndDoes(_ => operations.Add("commit"));
        dependencies.Commands.EnqueueAsync(Arg.Any<DietPlanUpdatedInAppNotificationCommand>())
            .Returns(Task.CompletedTask)
            .AndDoes(call =>
            {
                notification = call.Arg<DietPlanUpdatedInAppNotificationCommand>();
                operations.Add("notification");
            });

        var result = await dependencies.CreateService().CreateTraineePlanAsync(
            trainer,
            traineeId,
            ValidCommand());

        result.IsSuccess.Should().BeTrue();
        stagedPlan.Should().NotBeNull();
        stagedPlan!.TrainerId.Should().Be(trainer.Id);
        stagedPlan.TraineeId.Should().Be(traineeId);
        stagedPlan.Name.Should().Be("Nutrition plan");
        stagedHistory.Should().NotBeNull();
        stagedHistory!.DietPlanId.Should().Be(stagedPlan.Id);
        stagedHistory.ChangedByUserId.Should().Be(trainer.Id);
        stagedHistory.ChangeType.Should().Be("Created");
        notification.Should().NotBeNull();
        notification!.DietPlanId.Should().Be(stagedPlan.Id);
        notification.TrainerId.Should().Be(trainer.Id);
        notification.TraineeId.Should().Be(traineeId);
        operations.Should().Equal("access", "plan", "history", "commit", "notification");
        await dependencies.UnitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task CreateTraineePlanAsync_WhenRelationshipIsUnavailable_ReturnsNotFoundWithoutWrites(bool isTrainer)
    {
        var trainer = User(Id<User>.New());
        var traineeId = Id<User>.New();
        var dependencies = new Dependencies();
        dependencies.Access.GetAccessDecisionAsync(trainer.Id, traineeId, Arg.Any<CancellationToken>())
            .Returns(new CoachingRelationshipAccessDecision(isTrainer, false));

        var result = await dependencies.CreateService().CreateTraineePlanAsync(
            trainer,
            traineeId,
            ValidCommand());

        result.Error.Should().BeOfType<NotFoundError>();
        result.Error.Message.Should().Be(Messages.DidntFind);
        await dependencies.Access.Received(1).GetAccessDecisionAsync(
            trainer.Id,
            traineeId,
            Arg.Any<CancellationToken>());
        await AssertNoWritesAsync(dependencies);
    }

    [Test]
    public async Task CreateTraineePlanAsync_WhenTraineeIdIsEmpty_ReturnsBadRequestBeforeAccessWithoutWrites()
    {
        var trainer = User(Id<User>.New());
        var dependencies = new Dependencies();

        var result = await dependencies.CreateService().CreateTraineePlanAsync(
            trainer,
            Id<User>.Empty,
            ValidCommand());

        result.Error.Should().BeOfType<BadRequestError>();
        result.Error.Message.Should().Be(Messages.UserIdRequired);
        await dependencies.Access.DidNotReceive().GetAccessDecisionAsync(
            Arg.Any<Id<User>>(),
            Arg.Any<Id<User>>(),
            Arg.Any<CancellationToken>());
        await AssertNoWritesAsync(dependencies);
    }

    private static async Task AssertNoWritesAsync(Dependencies dependencies)
    {
        await dependencies.Plans.DidNotReceive().AddPlanAsync(
            Arg.Any<DietPlan>(),
            Arg.Any<CancellationToken>());
        await dependencies.Plans.DidNotReceive().AddHistoryEntryAsync(
            Arg.Any<DietPlanHistory>(),
            Arg.Any<CancellationToken>());
        await dependencies.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await dependencies.UnitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
        await dependencies.Commands.DidNotReceive().EnqueueAsync(
            Arg.Any<DietPlanUpdatedInAppNotificationCommand>());
    }

    private static UpsertDietPlanCommand ValidCommand()
        => new()
        {
            Name = " Nutrition plan ",
            StartDate = new DateOnly(2026, 7, 23),
            IsActive = true,
            Meals =
            [
                new UpsertDietMealCommand
                {
                    Name = " Breakfast ",
                    Order = 0,
                    EstimatedCalories = 500
                }
            ]
        };

    private static User User(Id<User> id)
        => new()
        {
            Id = id,
            Name = $"user-{id}",
            Email = $"{id}@example.com"
        };

    private sealed class Dependencies
    {
        public ICoachingRelationshipAccessService Access { get; } = Substitute.For<ICoachingRelationshipAccessService>();
        public IDietPlanRepository Plans { get; } = Substitute.For<IDietPlanRepository>();
        public ICommandDispatcher Commands { get; } = Substitute.For<ICommandDispatcher>();
        public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

        public DietPlanService CreateService()
            => new(Plans, Access, Commands, UnitOfWork);
    }
}
