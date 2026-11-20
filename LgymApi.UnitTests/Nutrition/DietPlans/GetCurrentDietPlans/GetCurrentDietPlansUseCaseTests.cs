using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Nutrition.DietPlans.GetCurrentDietPlans;
using LgymApi.Application.Nutrition.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.UnitTests.Nutrition.DietPlans.GetCurrentDietPlans;

[TestFixture]
public sealed class GetCurrentDietPlansUseCaseTests
{
    [Test]
    public async Task ExecuteAsync_WhenMultipleActivePlansExist_PreservesPersistenceOrder()
    {
        var traineeId = Id<UserEntity>.New();
        var plans = new List<DietPlan>
        {
            CreatePlan(traineeId, "Updated latest", true, false, new DateOnly(2026, 7, 2), new DateTimeOffset(2026, 7, 4, 8, 0, 0, TimeSpan.Zero)),
            CreatePlan(traineeId, "Same update later start", true, false, new DateOnly(2026, 7, 4), new DateTimeOffset(2026, 7, 3, 8, 0, 0, TimeSpan.Zero)),
            CreatePlan(traineeId, "Same update earlier start", true, false, new DateOnly(2026, 7, 3), new DateTimeOffset(2026, 7, 3, 8, 0, 0, TimeSpan.Zero))
        };
        var dependencies = new Dependencies();
        dependencies.Plans.ListActivePlansForTraineeAsync(traineeId, CancellationToken.None).Returns(plans);

        var result = await dependencies.CreateUseCase().ExecuteAsync(new GetCurrentDietPlansQuery(traineeId));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Select(plan => plan.Name), Is.EqualTo(plans.Select(plan => plan.Name)));
        Assert.That(result.Value.Select(plan => plan.UpdatedAt), Is.EqualTo(plans.Select(plan => plan.UpdatedAt)));
        Assert.That(result.Value.Select(plan => plan.StartDate), Is.EqualTo(plans.Select(plan => plan.StartDate)));
        await dependencies.Plans.Received(1).ListActivePlansForTraineeAsync(traineeId, CancellationToken.None);
        Assert.That(dependencies.Plans.ReceivedCalls(), Has.Exactly(1).Items);
    }

    [Test]
    public async Task ExecuteAsync_WhenPersistenceExcludesInactiveAndDeletedRows_ReturnsOnlyVisibleActivePlans()
    {
        var traineeId = Id<UserEntity>.New();
        var active = CreatePlan(traineeId, "Active", true, false, new DateOnly(2026, 7, 1), DateTimeOffset.UtcNow);
        var inactive = CreatePlan(traineeId, "Inactive", false, false, new DateOnly(2026, 7, 2), DateTimeOffset.UtcNow);
        var deleted = CreatePlan(traineeId, "Deleted", true, true, new DateOnly(2026, 7, 3), DateTimeOffset.UtcNow);
        var dependencies = new Dependencies();
        dependencies.Plans.ListActivePlansForTraineeAsync(traineeId, CancellationToken.None)
            .Returns(new List<DietPlan> { active });

        var result = await dependencies.CreateUseCase().ExecuteAsync(new GetCurrentDietPlansQuery(traineeId));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Select(plan => plan.Id), Is.EqualTo(new[] { active.Id }));
        Assert.That(result.Value.Select(plan => plan.Name), Has.None.EqualTo(inactive.Name).And.None.EqualTo(deleted.Name));
    }

    [Test]
    public async Task ExecuteAsync_WhenNoActivePlansExist_ReturnsSuccessfulEmptyList()
    {
        var traineeId = Id<UserEntity>.New();
        var dependencies = new Dependencies();
        dependencies.Plans.ListActivePlansForTraineeAsync(traineeId, CancellationToken.None).Returns([]);

        var result = await dependencies.CreateUseCase().ExecuteAsync(new GetCurrentDietPlansQuery(traineeId));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.Empty);
        await dependencies.Plans.Received(1).ListActivePlansForTraineeAsync(traineeId, CancellationToken.None);
    }

    [Test]
    public async Task ExecuteAsync_ForwardsCancellationToNoTrackingPersistence()
    {
        var traineeId = Id<UserEntity>.New();
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var dependencies = new Dependencies();
        dependencies.Plans.ListActivePlansForTraineeAsync(traineeId, cancellationToken).Returns([]);

        var result = await dependencies.CreateUseCase().ExecuteAsync(
            new GetCurrentDietPlansQuery(traineeId),
            cancellationToken);

        Assert.That(result.IsSuccess, Is.True);
        await dependencies.Plans.Received(1).ListActivePlansForTraineeAsync(traineeId, cancellationToken);
        Assert.That(dependencies.Plans.ReceivedCalls(), Has.Exactly(1).Items);
    }

    private static DietPlan CreatePlan(
        Id<UserEntity> traineeId,
        string name,
        bool isActive,
        bool isDeleted,
        DateOnly startDate,
        DateTimeOffset updatedAt)
        => new()
        {
            Id = Id<DietPlan>.New(),
            TraineeId = traineeId,
            Name = name,
            StartDate = startDate,
            IsActive = isActive,
            IsDeleted = isDeleted,
            CreatedAt = updatedAt,
            UpdatedAt = updatedAt
        };

    private sealed class Dependencies
    {
        public IDietPlanPersistence Plans { get; } = Substitute.For<IDietPlanPersistence>();
        public IMapper Mapper { get; } = CreateMapper();

        public IGetCurrentDietPlansUseCase CreateUseCase()
            => new GetCurrentDietPlansUseCase(Plans, Mapper);

        private static IMapper CreateMapper()
        {
            var services = new ServiceCollection();
            services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);

            using var provider = services.BuildServiceProvider();
            return provider.GetRequiredService<IMapper>();
        }
    }
}
