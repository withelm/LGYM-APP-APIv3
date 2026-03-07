using LgymApi.Application.Repositories;

namespace LgymApi.Application.Features.PlanDay;

public interface IPlanDayServiceDependencies
{
    IPlanRepository PlanRepository { get; }
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
        IPlanDayRepository planDayRepository,
        IPlanDayExerciseRepository planDayExerciseRepository,
        IExerciseRepository exerciseRepository,
        ITrainingRepository trainingRepository,
        IUnitOfWork unitOfWork)
    {
        PlanRepository = planRepository;
        PlanDayRepository = planDayRepository;
        PlanDayExerciseRepository = planDayExerciseRepository;
        ExerciseRepository = exerciseRepository;
        TrainingRepository = trainingRepository;
        UnitOfWork = unitOfWork;
    }

    public IPlanRepository PlanRepository { get; }
    public IPlanDayRepository PlanDayRepository { get; }
    public IPlanDayExerciseRepository PlanDayExerciseRepository { get; }
    public IExerciseRepository ExerciseRepository { get; }
    public ITrainingRepository TrainingRepository { get; }
    public IUnitOfWork UnitOfWork { get; }
}
