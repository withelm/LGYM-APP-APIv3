using FluentAssertions;
using LgymApi.Application.Coaching.Contracts.Access;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Features.Supplementation;
using LgymApi.Application.Features.Supplementation.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class SupplementationServiceTests
{
    [Test]
    public async Task CreateTraineePlanAsync_WhenLinked_StagesOwnerPlanAndCommitsOnce()
    {
        var trainer = User(Id<User>.New());
        var traineeId = Id<User>.New();
        var operations = new List<string>();
        var dependencies = new Dependencies();
        SupplementPlan? stagedPlan = null;
        dependencies.Access.GetAccessDecisionAsync(trainer.Id, traineeId, Arg.Any<CancellationToken>())
            .Returns(new CoachingRelationshipAccessDecision(true, true))
            .AndDoes(_ => operations.Add("access"));
        dependencies.Plans.AddPlanAsync(Arg.Any<SupplementPlan>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(call =>
            {
                stagedPlan = call.Arg<SupplementPlan>();
                operations.Add("plan");
            });
        dependencies.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(1)
            .AndDoes(_ => operations.Add("commit"));

        var result = await dependencies.CreateService().CreateTraineePlanAsync(
            trainer,
            traineeId,
            ValidCommand());

        result.IsSuccess.Should().BeTrue();
        stagedPlan.Should().NotBeNull();
        stagedPlan!.TrainerId.Should().Be(trainer.Id);
        stagedPlan.TraineeId.Should().Be(traineeId);
        stagedPlan.Name.Should().Be("Daily supplements");
        stagedPlan.Items.Should().ContainSingle().Which.SupplementName.Should().Be("Magnesium");
        operations.Should().Equal("access", "plan", "commit");
        await dependencies.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await dependencies.UnitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetTraineePlansAsync_WhenUserIsNotTrainer_ReturnsForbiddenWithoutNutritionAccess()
    {
        var trainer = User(Id<User>.New());
        var traineeId = Id<User>.New();
        var dependencies = new Dependencies();
        dependencies.Access.GetAccessDecisionAsync(trainer.Id, traineeId, Arg.Any<CancellationToken>())
            .Returns(new CoachingRelationshipAccessDecision(false, false));

        var result = await dependencies.CreateService().GetTraineePlansAsync(trainer, traineeId);

        result.Error.Should().BeOfType<SupplementationForbiddenError>();
        result.Error.Message.Should().Be(Messages.TrainerRoleRequired);
        await AssertNoNutritionAccessOrWritesAsync(dependencies);
    }

    [Test]
    public async Task GetTraineePlansAsync_WhenTrainerHasNoRelationship_ReturnsNotFoundWithoutNutritionAccess()
    {
        var trainer = User(Id<User>.New());
        var traineeId = Id<User>.New();
        var dependencies = new Dependencies();
        dependencies.Access.GetAccessDecisionAsync(trainer.Id, traineeId, Arg.Any<CancellationToken>())
            .Returns(new CoachingRelationshipAccessDecision(true, false));

        var result = await dependencies.CreateService().GetTraineePlansAsync(trainer, traineeId);

        result.Error.Should().BeOfType<SupplementationNotFoundError>();
        result.Error.Message.Should().Be(Messages.DidntFind);
        await AssertNoNutritionAccessOrWritesAsync(dependencies);
    }

    [Test]
    public async Task GetTraineePlansAsync_WhenTraineeIdIsEmptyForTrainer_ReturnsInvalidAfterTrainerCheckWithoutWrites()
    {
        var trainer = User(Id<User>.New());
        var dependencies = new Dependencies();
        dependencies.Access.GetAccessDecisionAsync(trainer.Id, Id<User>.Empty, Arg.Any<CancellationToken>())
            .Returns(new CoachingRelationshipAccessDecision(true, false));

        var result = await dependencies.CreateService().GetTraineePlansAsync(trainer, Id<User>.Empty);

        result.Error.Should().BeOfType<InvalidSupplementationError>();
        result.Error.Message.Should().Be(Messages.UserIdRequired);
        await dependencies.Access.Received(1).GetAccessDecisionAsync(
            trainer.Id,
            Id<User>.Empty,
            Arg.Any<CancellationToken>());
        await AssertNoNutritionAccessOrWritesAsync(dependencies);
    }

    [Test]
    public async Task GetTraineePlansAsync_WhenTraineeIdIsEmptyForNonTrainer_ReturnsForbiddenBeforeInvalidId()
    {
        var trainer = User(Id<User>.New());
        var dependencies = new Dependencies();
        dependencies.Access.GetAccessDecisionAsync(trainer.Id, Id<User>.Empty, Arg.Any<CancellationToken>())
            .Returns(new CoachingRelationshipAccessDecision(false, false));

        var result = await dependencies.CreateService().GetTraineePlansAsync(trainer, Id<User>.Empty);

        result.Error.Should().BeOfType<SupplementationForbiddenError>();
        result.Error.Message.Should().Be(Messages.TrainerRoleRequired);
        await AssertNoNutritionAccessOrWritesAsync(dependencies);
    }

    private static async Task AssertNoNutritionAccessOrWritesAsync(Dependencies dependencies)
    {
        await dependencies.Plans.DidNotReceive().GetPlansByTrainerAndTraineeAsync(
            Arg.Any<Id<User>>(),
            Arg.Any<Id<User>>(),
            Arg.Any<CancellationToken>());
        await dependencies.Plans.DidNotReceive().AddPlanAsync(
            Arg.Any<SupplementPlan>(),
            Arg.Any<CancellationToken>());
        await dependencies.Plans.DidNotReceive().AddIntakeLogAsync(
            Arg.Any<SupplementIntakeLog>(),
            Arg.Any<CancellationToken>());
        await dependencies.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await dependencies.UnitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
    }

    private static UpsertSupplementPlanCommand ValidCommand()
        => new()
        {
            Name = " Daily supplements ",
            Notes = " Before sleep ",
            Items =
            [
                new UpsertSupplementPlanItemCommand
                {
                    SupplementName = " Magnesium ",
                    Dosage = " 1 tablet ",
                    TimeOfDay = "21:00",
                    DaysOfWeekMask = 127,
                    Order = 0
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
        public ISupplementationRepository Plans { get; } = Substitute.For<ISupplementationRepository>();
        public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

        public SupplementationService CreateService()
            => new(Access, Plans, UnitOfWork);
    }
}
