using LgymApi.Application.Repositories;
using LgymApi.Application.TrainingPlanning.Contracts.PlanDay;

namespace LgymApi.Application.Features.PlanDay;

public interface IPlanDayServiceDependencies
{
    IPlanRepository PlanRepository { get; }
    IPlanDayRelationshipAccessPort RelationshipAccess { get; }
    IPlanDayRepository PlanDayRepository { get; }
    IPlanDayExerciseRepository PlanDayExerciseRepository { get; }
    IExerciseRepository ExerciseRepository { get; }
    ITrainingRepository TrainingRepository { get; }
    IUnitOfWork UnitOfWork { get; }
}

internal sealed class PlanDayServiceDependencies : IPlanDayServiceDependencies
{
    public PlanDayServiceDependencies(
        IPlanRepository planRepository,
        IPlanDayRelationshipAccessPort relationshipAccess,
        IPlanDayRepository planDayRepository,
        IPlanDayExerciseRepository planDayExerciseRepository,
        IExerciseRepository exerciseRepository,
        ITrainingRepository trainingRepository,
        IUnitOfWork unitOfWork)
    {
        PlanRepository = planRepository;
        RelationshipAccess = relationshipAccess;
        PlanDayRepository = planDayRepository;
        PlanDayExerciseRepository = planDayExerciseRepository;
        ExerciseRepository = exerciseRepository;
        TrainingRepository = trainingRepository;
        UnitOfWork = unitOfWork;
    }

    public IPlanRepository PlanRepository { get; }
    public IPlanDayRelationshipAccessPort RelationshipAccess { get; }
    public IPlanDayRepository PlanDayRepository { get; }
    public IPlanDayExerciseRepository PlanDayExerciseRepository { get; }
    public IExerciseRepository ExerciseRepository { get; }
    public ITrainingRepository TrainingRepository { get; }
    public IUnitOfWork UnitOfWork { get; }
}
