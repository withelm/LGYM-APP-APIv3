using System.Text.Json;
using LgymApi.Application.Coaching.Contracts.Access;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Coaching.Errors;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Nutrition.Contracts.BackgroundCommands;
using LgymApi.Application.Nutrition.DietPlans;
using LgymApi.Application.Nutrition.DietPlans.CreateTraineePlan;
using LgymApi.Application.Nutrition.DietPlans.Models;
using LgymApi.Application.Nutrition.Persistence;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests.Nutrition.DietPlans.CreateTraineePlan;

[TestFixture]
public sealed class CreateTraineeDietPlanUseCaseTests
{
    [Test]
    public async Task ActiveCreate_NormalizesStagesHistorySavesThenEnqueues()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var cancellationToken = new CancellationTokenSource().Token;
        var operations = new List<string>();
        var dependencies = new Dependencies();
        DietPlan? stagedPlan = null;
        DietPlanHistory? stagedHistory = null;
        DietPlanUpdatedInAppNotificationCommand? notification = null;
        dependencies.GrantAccess(trainerId, traineeId, operations);
        dependencies.Plans.AddPlanAsync(Arg.Any<DietPlan>(), cancellationToken)
            .Returns(Task.CompletedTask)
            .AndDoes(call =>
            {
                stagedPlan = call.Arg<DietPlan>();
                operations.Add("stage plan");
            });
        dependencies.Plans.AddHistoryEntryAsync(Arg.Any<DietPlanHistory>(), cancellationToken)
            .Returns(Task.CompletedTask)
            .AndDoes(call =>
            {
                stagedHistory = call.Arg<DietPlanHistory>();
                operations.Add("stage history");
            });
        dependencies.UnitOfWork.SaveChangesAsync(cancellationToken)
            .Returns(Task.FromResult(2))
            .AndDoes(_ => operations.Add("save"));
        dependencies.Commands.EnqueueAsync(Arg.Any<DietPlanUpdatedInAppNotificationCommand>())
            .Returns(Task.CompletedTask)
            .AndDoes(call =>
            {
                notification = call.Arg<DietPlanUpdatedInAppNotificationCommand>();
                operations.Add("enqueue");
            });
        var beforeNotification = DateTimeOffset.UtcNow;

        var result = await dependencies.CreateUseCase().ExecuteAsync(
            new CreateTraineeDietPlanCommand(NutritionAuthorizationTestFactory.CreateActor(trainerId), trainerId, traineeId, ValidData()),
            cancellationToken);

        var afterNotification = DateTimeOffset.UtcNow;
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Name, Is.EqualTo("Cut"));
        Assert.That(result.Value.Notes, Is.Null);
        Assert.That(result.Value.Meals.Select(meal => meal.Name), Is.EqualTo(new[] { "Breakfast", "Dinner" }));
        Assert.That(stagedPlan, Is.Not.Null);
        Assert.That(stagedPlan!.Id.IsEmpty, Is.False);
        Assert.That(stagedPlan.TrainerId, Is.EqualTo(trainerId));
        Assert.That(stagedPlan.TraineeId, Is.EqualTo(traineeId));
        Assert.That(stagedPlan.IsDeleted, Is.False);
        Assert.That((stagedPlan.Meals).All(meal => !meal.Id.IsEmpty && meal.DietPlanId == stagedPlan.Id), Is.True);
        Assert.That(stagedPlan.Meals.Select(meal => meal.Description), Is.EqualTo(new[] { "eggs", null }));
        Assert.That(stagedHistory, Is.Not.Null);
        Assert.That(stagedHistory!.Id.IsEmpty, Is.False);
        Assert.That(stagedHistory.DietPlanId, Is.EqualTo(stagedPlan.Id));
        Assert.That(stagedHistory.ChangedByUserId, Is.EqualTo(trainerId));
        Assert.That(stagedHistory.ChangeType, Is.EqualTo("Created"));
        using (var snapshot = JsonDocument.Parse(stagedHistory.SnapshotJson))
        {
            Assert.That(snapshot.RootElement.GetProperty("Name").GetString(), Is.EqualTo("Cut"));
            Assert.That(snapshot.RootElement.GetProperty("Meals").EnumerateArray()
                .Select(meal => meal.GetProperty("Name").GetString()), Is.EqualTo(new[] { "Breakfast", "Dinner" }));
        }

        Assert.That(notification, Is.Not.Null);
        Assert.That(notification!.DietPlanId, Is.EqualTo(stagedPlan.Id));
        Assert.That(notification.TraineeId, Is.EqualTo(traineeId));
        Assert.That(notification.TrainerId, Is.EqualTo(trainerId));
        Assert.That(notification.DietPlanName, Is.EqualTo("Cut"));
        var assertedChain51 = notification.TriggeredAt;
        Assert.That(assertedChain51, Is.GreaterThanOrEqualTo(beforeNotification));
        Assert.That(assertedChain51, Is.LessThanOrEqualTo(afterNotification));
        Assert.That(operations, Is.EqualTo(new[] { "access", "stage plan", "stage history", "save", "enqueue" }));
        await dependencies.UnitOfWork.Received(1).SaveChangesAsync(cancellationToken);
        await dependencies.UnitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InactiveCreate_SavesWithoutEnqueueing()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var cancellationToken = new CancellationTokenSource().Token;
        var dependencies = new Dependencies();
        dependencies.GrantAccess(trainerId, traineeId);
        dependencies.Plans.AddPlanAsync(Arg.Any<DietPlan>(), cancellationToken).Returns(Task.CompletedTask);
        dependencies.Plans.AddHistoryEntryAsync(Arg.Any<DietPlanHistory>(), cancellationToken).Returns(Task.CompletedTask);
        dependencies.UnitOfWork.SaveChangesAsync(cancellationToken).Returns(Task.FromResult(2));

        var result = await dependencies.CreateUseCase().ExecuteAsync(
            new CreateTraineeDietPlanCommand(NutritionAuthorizationTestFactory.CreateActor(trainerId), trainerId, traineeId, ValidData(isActive: false)),
            cancellationToken);

        Assert.That(result.IsSuccess, Is.True);
        await dependencies.UnitOfWork.Received(1).SaveChangesAsync(cancellationToken);
        await dependencies.Commands.DidNotReceive().EnqueueAsync(Arg.Any<DietPlanUpdatedInAppNotificationCommand>());
        await dependencies.UnitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MacroOnlyCreate_AllowsNoMeals()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var cancellationToken = new CancellationTokenSource().Token;
        var dependencies = new Dependencies();
        DietPlan? stagedPlan = null;
        dependencies.GrantAccess(trainerId, traineeId);
        dependencies.Plans.AddPlanAsync(Arg.Any<DietPlan>(), cancellationToken)
            .Returns(Task.CompletedTask)
            .AndDoes(call => stagedPlan = call.Arg<DietPlan>());
        dependencies.Plans.AddHistoryEntryAsync(Arg.Any<DietPlanHistory>(), cancellationToken).Returns(Task.CompletedTask);
        dependencies.UnitOfWork.SaveChangesAsync(cancellationToken).Returns(Task.FromResult(2));

        var result = await dependencies.CreateUseCase().ExecuteAsync(
            new CreateTraineeDietPlanCommand(
                NutritionAuthorizationTestFactory.CreateActor(trainerId),
                trainerId,
                traineeId,
                new DietPlanUpsertData("Macros", new DateOnly(2026, 7, 23), null, 2200, null, null, null, null, false, [])),
            cancellationToken);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(stagedPlan!.Meals, Is.Empty);
        Assert.That(result.Value.Meals, Is.Empty);
        await dependencies.UnitOfWork.Received(1).SaveChangesAsync(cancellationToken);
        await dependencies.Commands.DidNotReceive().EnqueueAsync(Arg.Any<DietPlanUpdatedInAppNotificationCommand>());
        await dependencies.UnitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task NoMealAndNoMacro_ReturnsBadRequestAfterAccess()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var cancellationToken = new CancellationTokenSource().Token;
        var dependencies = new Dependencies();
        dependencies.GrantAccess(trainerId, traineeId);

        var result = await dependencies.CreateUseCase().ExecuteAsync(
            new CreateTraineeDietPlanCommand(
                NutritionAuthorizationTestFactory.CreateActor(trainerId),
                trainerId,
                traineeId,
                new DietPlanUpsertData("Plan", new DateOnly(2026, 7, 23), null, null, null, null, null, null, false, [])),
            cancellationToken);

        Assert.That(result.Error, Is.TypeOf<BadRequestError>());
        Assert.That(result.Error.Message, Is.EqualTo(Messages.FieldRequired));
        await dependencies.Access.Received(1).GetAccessDecisionAsync(trainerId, traineeId, cancellationToken);
        await AssertNoWritesAsync(dependencies);
    }

    [Test]
    public async Task InvalidNameStartOrMeal_ReturnsBadRequestAfterAccess()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var invalidBodies = new[]
        {
            ValidData(name: " "),
            ValidData(startDate: default(DateOnly)),
            ValidData(meals: [new DietMealInput(" ", 0, null, null, null, null, null)])
        };

        foreach (var data in invalidBodies)
        {
            var cancellationToken = new CancellationTokenSource().Token;
            var dependencies = new Dependencies();
            dependencies.GrantAccess(trainerId, traineeId);

            var result = await dependencies.CreateUseCase().ExecuteAsync(
                new CreateTraineeDietPlanCommand(NutritionAuthorizationTestFactory.CreateActor(trainerId), trainerId, traineeId, data),
                cancellationToken);

            Assert.That(result.Error, Is.TypeOf<BadRequestError>());
            Assert.That(result.Error.Message, Is.EqualTo(Messages.FieldRequired));
            await dependencies.Access.Received(1).GetAccessDecisionAsync(trainerId, traineeId, cancellationToken);
            await AssertNoWritesAsync(dependencies);
        }
    }

    [Test]
    public async Task EmptyTraineeId_ReturnsBadRequestWithoutAccess()
    {
        var trainerId = Id<User>.New();
        var dependencies = new Dependencies();

        var result = await dependencies.CreateUseCase().ExecuteAsync(
            new CreateTraineeDietPlanCommand(NutritionAuthorizationTestFactory.CreateActor(trainerId), trainerId, Id<User>.Empty, ValidData()));

        Assert.That(result.Error, Is.TypeOf<BadRequestError>());
        Assert.That(result.Error.Message, Is.EqualTo(Messages.UserIdRequired));
        await dependencies.Access.DidNotReceive().GetAccessDecisionAsync(
            Arg.Any<Id<User>>(),
            Arg.Any<Id<User>>(),
            Arg.Any<CancellationToken>());
        await AssertNoWritesAsync(dependencies);
    }

    [Test]
    public async Task NonTrainer_ReturnsForbiddenWithoutWrites()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var dependencies = new Dependencies();
        dependencies.Access.GetAccessDecisionAsync(trainerId, traineeId, Arg.Any<CancellationToken>())
            .Returns(new CoachingRelationshipAccessDecision(false, true));

        var result = await dependencies.CreateUseCase().ExecuteAsync(
            new CreateTraineeDietPlanCommand(NutritionAuthorizationTestFactory.CreateActor(trainerId), trainerId, traineeId, ValidData()));

        Assert.That(result.Error, Is.TypeOf<TrainerRelationshipForbiddenError>());
        Assert.That(result.Error.Message, Is.EqualTo(Messages.TrainerRoleRequired));
        await AssertNoWritesAsync(dependencies);
    }

    [Test]
    public async Task SaveFailure_PropagatesAndDoesNotEnqueue()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var cancellationToken = new CancellationTokenSource().Token;
        var operations = new List<string>();
        var dependencies = new Dependencies();
        dependencies.GrantAccess(trainerId, traineeId, operations);
        dependencies.Plans.AddPlanAsync(Arg.Any<DietPlan>(), cancellationToken)
            .Returns(Task.CompletedTask)
            .AndDoes(_ => operations.Add("stage plan"));
        dependencies.Plans.AddHistoryEntryAsync(Arg.Any<DietPlanHistory>(), cancellationToken)
            .Returns(Task.CompletedTask)
            .AndDoes(_ => operations.Add("stage history"));
        dependencies.UnitOfWork.SaveChangesAsync(cancellationToken)
            .Returns(Task.FromException<int>(new InvalidOperationException("save failed")))
            .AndDoes(_ => operations.Add("save"));

        Func<Task> act = () => dependencies.CreateUseCase().ExecuteAsync(
            new CreateTraineeDietPlanCommand(NutritionAuthorizationTestFactory.CreateActor(trainerId), trainerId, traineeId, ValidData()),
            cancellationToken);

        await Assert.ThatAsync(async () => { await act(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("save failed").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
        Assert.That(operations, Is.EqualTo(new[] { "access", "stage plan", "stage history", "save" }));
        await dependencies.UnitOfWork.Received(1).SaveChangesAsync(cancellationToken);
        await dependencies.Commands.DidNotReceive().EnqueueAsync(Arg.Any<DietPlanUpdatedInAppNotificationCommand>());
        await dependencies.UnitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
    }

    private static DietPlanUpsertData ValidData(
        string name = " Cut ",
        DateOnly? startDate = null,
        IEnumerable<DietMealInput>? meals = null,
        bool isActive = true)
        => new(
            name,
            startDate ?? new DateOnly(2026, 7, 23),
            null,
            2200,
            180m,
            220m,
            70m,
            " ",
            isActive,
            meals ??
            [
                new DietMealInput(" Dinner ", 2, " ", 900, 60m, 100m, 20m),
                new DietMealInput(" Breakfast ", -1, " eggs ", 600, 40m, 60m, 15m)
            ]);

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
        await dependencies.Commands.DidNotReceive().EnqueueAsync(Arg.Any<DietPlanUpdatedInAppNotificationCommand>());
    }

    private sealed class Dependencies
    {
        public ICoachingRelationshipAccessService Access { get; } = Substitute.For<ICoachingRelationshipAccessService>();
        public IDietPlanPersistence Plans { get; } = Substitute.For<IDietPlanPersistence>();
        public ICommandDispatcher Commands { get; } = Substitute.For<ICommandDispatcher>();
        public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();
        private IMapper Mapper { get; } = CreateMapper();

        public void GrantAccess(Id<User> trainerId, Id<User> traineeId, List<string>? operations = null)
            => Access.GetAccessDecisionAsync(trainerId, traineeId, Arg.Any<CancellationToken>())
                .Returns(new CoachingRelationshipAccessDecision(true, true))
                .AndDoes(_ => operations?.Add("access"));

        public ICreateTraineeDietPlanUseCase CreateUseCase()
            => new CreateTraineeDietPlanUseCase(
                Access,
                Plans,
                Commands,
                UnitOfWork,
                Mapper,
                new DietPlanHistorySnapshotFactory(Mapper),
                NutritionAuthorizationTestFactory.CreateDietCoordinator());

        private static IMapper CreateMapper()
        {
            var services = new ServiceCollection();
            services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);

            using var provider = services.BuildServiceProvider();
            return provider.GetRequiredService<IMapper>();
        }
    }
}
