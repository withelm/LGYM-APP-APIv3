using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.TrainingPlanning.Contracts.PlanDay;
using LgymApi.Application.TrainingPlanning.PlanDay;
using LgymApi.Application.TrainingPlanning.PlanDay.Persistence;
using LgymApi.Domain.ValueObjects;
using LgymApi.TrainingPlanning;
using LgymApi.TrainingPlanning.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class PlanDayReferenceReadServiceTests
{
    [Test]
    public async Task GetByIdsAsync_PreservesOrderDuplicatesAndExplicitMissingDeletedFacts()
    {
        var persistence = new PlanDayPersistenceFake();
        var firstId = Id<PlanDayReference>.New();
        var missingId = Id<PlanDayReference>.New();
        var deletedId = Id<PlanDayReference>.New();
        var secondId = Id<PlanDayReference>.New();
        persistence.PlanDaysByIds =
            [
                new PlanDayPersistenceModel(firstId, Id<PlanReference>.New(), "First", false),
                new PlanDayPersistenceModel(deletedId, Id<PlanReference>.New(), "Deleted", true),
                new PlanDayPersistenceModel(secondId, Id<PlanReference>.New(), "Second", false)
            ];
        var service = new PlanDayReferenceReadService(persistence, CreateMapper());

        var result = await service.GetByIdsAsync([secondId, missingId, deletedId, firstId, secondId]);

        Assert.That(result, Is.EqualTo(new[] { new PlanDayReferenceReadModel(secondId, result[0].PlanId, "Second", true, false), new PlanDayReferenceReadModel(missingId, Id<PlanReference>.Empty, string.Empty, false, false), new PlanDayReferenceReadModel(deletedId, result[2].PlanId, "Deleted", true, true), new PlanDayReferenceReadModel(firstId, result[3].PlanId, "First", true, false), new PlanDayReferenceReadModel(secondId, result[4].PlanId, "Second", true, false) }));
        Assert.That(persistence.GetPlanDaysByIdsCalls, Has.Exactly(1).Items);
        Assert.That(persistence.GetPlanDaysByIdsCalls[0].PlanDayIds, Is.EqualTo(new[] { secondId, missingId, deletedId, firstId, secondId }));
        Assert.That(persistence.GetPlanDaysByIdsCalls[0].CancellationToken, Is.EqualTo(CancellationToken.None));
    }

    [Test]
    public async Task GetByIdAsync_ReturnsNullWhenPlanDayIsUnavailable()
    {
        var persistence = new PlanDayPersistenceFake();
        var service = new PlanDayReferenceReadService(persistence, CreateMapper());

        var result = await service.GetByIdAsync(Id<PlanDayReference>.New());

        Assert.That(result, Is.EqualTo(new PlanDayReferenceReadModel(result.PlanDayId, Id<PlanReference>.Empty, string.Empty, false, false)));
    }

    [Test]
    public void TrainingPlanningModule_RegistersPlanDayReferenceReadServiceExactlyOnce()
    {
        var services = new ServiceCollection();
        services.AddTrainingPlanningModule();

        Assert.That(services.Count(descriptor => descriptor.ServiceType == typeof(IPlanDayReferenceReadService)), Is.EqualTo(1));
        Assert.That(services.Single(descriptor => descriptor.ServiceType == typeof(IPlanDayReferenceReadService)).ImplementationType, Is.EqualTo(typeof(PlanDayReferenceReadService)));
    }

    private static IMapper CreateMapper()
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);
        return services.BuildServiceProvider().GetRequiredService<IMapper>();
    }
}
