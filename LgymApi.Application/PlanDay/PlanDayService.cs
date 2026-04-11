using LgymApi.Application.Repositories;
using LgymApi.Domain.ValueObjects;
using PlanDayEntity = LgymApi.Domain.Entities.PlanDay;

namespace LgymApi.Application.Features.PlanDay;

public sealed partial class PlanDayService : IPlanDayService
{
    private readonly IPlanRepository _planRepository;
    private readonly IPlanDayRepository _planDayRepository;
    private readonly IPlanDayExerciseRepository _planDayExerciseRepository;
    private readonly IExerciseRepository _exerciseRepository;
    private readonly ITrainingRepository _trainingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PlanDayService(IPlanDayServiceDependencies dependencies)
    {
        _planRepository = dependencies.PlanRepository;
        _planDayRepository = dependencies.PlanDayRepository;
        _planDayExerciseRepository = dependencies.PlanDayExerciseRepository;
        _exerciseRepository = dependencies.ExerciseRepository;
        _trainingRepository = dependencies.TrainingRepository;
        _unitOfWork = dependencies.UnitOfWork;
    }
}
