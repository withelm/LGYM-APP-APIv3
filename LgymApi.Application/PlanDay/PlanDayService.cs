using LgymApi.Application.Repositories;
using LgymApi.Domain.ValueObjects;
using PlanDayEntity = LgymApi.Domain.Entities.PlanDay;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.PlanDay;

public sealed partial class PlanDayService : IPlanDayService
{
    private readonly IPlanRepository _planRepository;
    private readonly ITrainerRelationshipRepository _trainerRelationshipRepository;
    private readonly IPlanDayRepository _planDayRepository;
    private readonly IPlanDayExerciseRepository _planDayExerciseRepository;
    private readonly IExerciseRepository _exerciseRepository;
    private readonly ITrainingRepository _trainingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PlanDayService(IPlanDayServiceDependencies dependencies)
    {
        _planRepository = dependencies.PlanRepository;
        _trainerRelationshipRepository = dependencies.TrainerRelationshipRepository;
        _planDayRepository = dependencies.PlanDayRepository;
        _planDayExerciseRepository = dependencies.PlanDayExerciseRepository;
        _exerciseRepository = dependencies.ExerciseRepository;
        _trainingRepository = dependencies.TrainingRepository;
        _unitOfWork = dependencies.UnitOfWork;
    }

    private async Task<bool> CanAccessPlanAsync(UserEntity currentUser, Id<UserEntity> planOwnerId, CancellationToken cancellationToken)
    {
        if (planOwnerId == currentUser.Id)
        {
            return true;
        }

        var link = await _trainerRelationshipRepository.FindActiveLinkByTrainerAndTraineeAsync(
            currentUser.Id,
            planOwnerId,
            cancellationToken);

        return link != null;
    }
}
