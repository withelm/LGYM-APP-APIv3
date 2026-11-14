using LgymApi.Application.Mapping.Core;
using LgymApi.Application.TrainingPlanning.Contracts.PlanDay;
using LgymApi.Application.TrainingPlanning.PlanDay.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.TrainingPlanning.Contracts;
using PlanDayEntity = LgymApi.Domain.Entities.PlanDay;
using PlanEntity = LgymApi.Domain.Entities.Plan;

namespace LgymApi.Application.TrainingPlanning.Mapping;

public sealed class PlanDayMappingProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<PlanEntity, PlanDayPlanPersistenceModel>((source, _) =>
            new PlanDayPlanPersistenceModel(source.Id.Rebind<PlanReference>(), source.UserId.Rebind<AccountReference>(), source.Purpose));
        configuration.CreateMap<PlanDayEntity, PlanDayPersistenceModel>((source, _) =>
            new PlanDayPersistenceModel(source.Id.Rebind<PlanDayReference>(), source.PlanId.Rebind<PlanReference>(), source.Name, source.IsDeleted));
        configuration.CreateMap<PlanDayPersistenceModel, PlanDayReferenceReadModel>((source, _) =>
            new PlanDayReferenceReadModel(source.Id, source.PlanId, source.Name, true, source.IsDeleted));
        configuration.CreateMap<PlanDayReferenceSource, PlanDayReferenceReadModel>((source, _) =>
            new PlanDayReferenceReadModel(source.Day.Id, source.Day.PlanId, source.Day.Name, true, source.Day.IsDeleted,
                source.Plan?.OwnerId ?? Id<AccountReference>.Empty, source.Plan?.Purpose ?? LgymApi.Domain.Enums.PlanPurpose.Unclassified));
        configuration.CreateMap<PlanDayExercise, PlanDayExercisePersistenceModel>((source, _) =>
            new PlanDayExercisePersistenceModel(
                source.PlanDayId.Rebind<PlanDayReference>(),
                source.ExerciseId.Rebind<PlanExerciseReference>(),
                source.Order,
                source.Series,
                source.Reps));
        configuration.CreateMap<PlanDayExerciseWriteModel, PlanDayExercise>((source, _) =>
            new PlanDayExercise
            {
                ExerciseId = source.ExerciseId.Rebind<Exercise>(),
                Series = source.Series,
                Reps = source.Reps
            });
    }
}
