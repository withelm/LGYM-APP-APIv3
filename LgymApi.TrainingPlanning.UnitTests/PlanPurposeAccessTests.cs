using FluentAssertions;
using LgymApi.Application.Repositories;
using LgymApi.Application.TrainingPlanning.Contracts.PlanDay;
using LgymApi.Application.TrainingPlanning.PlanDay;
using LgymApi.Application.TrainingPlanning.PlanDay.Persistence;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.TrainingPlanning.Contracts;
using NSubstitute;

namespace LgymApi.TrainingPlanning.UnitTests;

[TestFixture]
public sealed class PlanPurposeAccessTests
{
    [TestCase(PlanPurpose.Unclassified)]
    [TestCase(PlanPurpose.Professional)]
    public async Task LinkedCoachCannotCreateDayOnNonPersonalPlan(PlanPurpose purpose)
    {
        var owner = Id<AccountReference>.New();
        var coach = Id<AccountReference>.New();
        var plan = Id<PlanReference>.New();
        var persistence = new DeniedPlanPersistence(new PlanDayPlanPersistenceModel(plan, owner, purpose));
        var access = Substitute.For<IPlanDayRelationshipAccessPort>();
        access.AuthorizeWriteAsync(coach, owner, Arg.Any<CancellationToken>()).Returns(true);
        var uow = Substitute.For<IUnitOfWork>();
        var service = new PlanDayService(persistence, access, Substitute.For<IPlanExerciseCatalogPort>(),
            Substitute.For<IPlanTrainingActivityPort>(), uow);
        var input = new PlanDayWriteModel("Day", [new PlanDayExerciseWriteModel(Id<PlanExerciseReference>.New(), 3, "8")]);

        var result = await service.CreateAsync(new CreatePlanDayCommand(coach, plan, input));

        result.IsFailure.Should().BeTrue();
        persistence.Created.Should().BeFalse();
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync();
    }

    private sealed class DeniedPlanPersistence(PlanDayPlanPersistenceModel plan) : IPlanDayPersistence
    {
        public bool Created { get; private set; }
        public Task<PlanDayPlanPersistenceModel?> FindPlanAsync(Id<PlanReference> id, CancellationToken cancellationToken = default) => Task.FromResult<PlanDayPlanPersistenceModel?>(id == plan.Id ? plan : null);
        public Task StagePlanRevisionAsync(Id<PlanReference> id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CreatePlanDayAsync(Id<PlanReference> id, PlanDayWriteModel input, CancellationToken cancellationToken = default) { Created = true; return Task.CompletedTask; }
        public Task<PlanDayPlanPersistenceModel?> FindActivePlanAsync(Id<AccountReference> id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PlanDayPersistenceModel?> FindPlanDayAsync(Id<PlanDayReference> id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlanDayPersistenceModel>> GetPlanDaysAsync(Id<PlanReference> id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlanDayPersistenceModel>> GetPlanDaysByIdsAsync(IReadOnlyCollection<Id<PlanDayReference>> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlanDayExercisePersistenceModel>> GetPlanDayExercisesAsync(IReadOnlyCollection<Id<PlanDayReference>> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdatePlanDayAsync(Id<PlanDayReference> id, string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ReplacePlanDayExercisesAsync(Id<PlanDayReference> id, IReadOnlyList<PlanDayExerciseWriteModel> exercises, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task MarkPlanDayDeletedAsync(Id<PlanDayReference> id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
