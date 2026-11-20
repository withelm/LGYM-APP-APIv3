using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Nutrition.DietPlans.GetCurrentDietPlan;
using LgymApi.Application.Nutrition.DietPlans.Models;
using LgymApi.Application.Nutrition.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using NSubstitute;

namespace LgymApi.UnitTests.Nutrition.DietPlans.GetCurrentDietPlan;

[TestFixture]
public sealed class GetCurrentDietPlanUseCaseTests
{
    [Test]
    public async Task ExecuteAsync_WhenActivePlanExists_ReturnsTheNewestMappedPlan()
    {
        var traineeId = Id<User>.New();
        var plan = CreatePlan(traineeId, "Newest active");
        var mappedPlan = CreateReadModel(plan);
        var dependencies = new Dependencies();
        dependencies.Plans.GetActivePlanForTraineeAsync(traineeId, CancellationToken.None).Returns(plan);
        dependencies.Mapper.Map<DietPlan, DietPlanReadModel>(plan, Arg.Any<MappingContext?>()).Returns(mappedPlan);

        var result = await dependencies.CreateUseCase().ExecuteAsync(new GetCurrentDietPlanQuery(traineeId));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.SameAs(mappedPlan));
        await dependencies.Plans.Received(1).GetActivePlanForTraineeAsync(traineeId, CancellationToken.None);
        Assert.That(dependencies.Plans.ReceivedCalls(), Has.Exactly(1).Items);
    }

    [Test]
    public async Task ExecuteAsync_WhenNoActivePlanExists_ReturnsLocalizedNotFound()
    {
        var traineeId = Id<User>.New();
        var dependencies = new Dependencies();
        dependencies.Plans.GetActivePlanForTraineeAsync(traineeId, CancellationToken.None).Returns((DietPlan?)null);

        var result = await dependencies.CreateUseCase().ExecuteAsync(new GetCurrentDietPlanQuery(traineeId));

        Assert.That(result.Error, Is.TypeOf<NotFoundError>());
        Assert.That(result.Error.Message, Is.EqualTo(Messages.DidntFind));
        Assert.That(dependencies.Mapper.ReceivedCalls(), Is.Empty);
    }

    [Test]
    public async Task ExecuteAsync_ForwardsCancellationToPersistence()
    {
        var traineeId = Id<User>.New();
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var dependencies = new Dependencies();
        dependencies.Plans.GetActivePlanForTraineeAsync(traineeId, cancellationToken).Returns((DietPlan?)null);

        await dependencies.CreateUseCase().ExecuteAsync(
            new GetCurrentDietPlanQuery(traineeId),
            cancellationToken);

        await dependencies.Plans.Received(1).GetActivePlanForTraineeAsync(traineeId, cancellationToken);
    }

    [Test]
    public async Task ExecuteAsync_WhenPlanIsReturned_PerformsOnlyReadAndMapping()
    {
        var traineeId = Id<User>.New();
        var plan = CreatePlan(traineeId, "Active");
        var dependencies = new Dependencies();
        dependencies.Plans.GetActivePlanForTraineeAsync(traineeId, CancellationToken.None).Returns(plan);
        dependencies.Mapper.Map<DietPlan, DietPlanReadModel>(plan, Arg.Any<MappingContext?>())
            .Returns(CreateReadModel(plan));

        await dependencies.CreateUseCase().ExecuteAsync(new GetCurrentDietPlanQuery(traineeId));

        await dependencies.Plans.DidNotReceiveWithAnyArgs().AddPlanAsync(default!, default);
        await dependencies.Plans.DidNotReceiveWithAnyArgs().AddHistoryEntryAsync(default!, default);
        await dependencies.Plans.DidNotReceiveWithAnyArgs().FindTrackedPlanByIdAsync(default, default);
        dependencies.Mapper.Received(1).Map<DietPlan, DietPlanReadModel>(plan, Arg.Any<MappingContext?>());
    }

    private static DietPlan CreatePlan(Id<User> traineeId, string name)
        => new()
        {
            Id = Id<DietPlan>.New(),
            TraineeId = traineeId,
            Name = name,
            StartDate = new DateOnly(2026, 7, 24),
            IsActive = true
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

    private sealed class Dependencies
    {
        public IDietPlanPersistence Plans { get; } = Substitute.For<IDietPlanPersistence>();
        public IMapper Mapper { get; } = Substitute.For<IMapper>();

        public IGetCurrentDietPlanUseCase CreateUseCase()
            => new GetCurrentDietPlanUseCase(Plans, Mapper);
    }
}
