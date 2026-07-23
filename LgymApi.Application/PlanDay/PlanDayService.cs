using LgymApi.Application.Repositories;
using LgymApi.Application.TrainingPlanning.Contracts.PlanDay;
using LgymApi.Domain.ValueObjects;
using PlanDayEntity = LgymApi.Domain.Entities.PlanDay;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.PlanDay;

public sealed partial class PlanDayService : IPlanDayService
{
    private readonly IPlanRepository _planRepository;
    private readonly IPlanDayRelationshipAccessPort _relationshipAccess;
    private readonly IPlanDayRepository _planDayRepository;
    private readonly IPlanDayExerciseRepository _planDayExerciseRepository;
    private readonly IExerciseRepository _exerciseRepository;
    private readonly ITrainingRepository _trainingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PlanDayService(IPlanDayServiceDependencies dependencies)
    {
        _planRepository = dependencies.PlanRepository;
        _relationshipAccess = dependencies.RelationshipAccess;
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

        return await _relationshipAccess.HasActiveRelationshipAsync(
            currentUser.Id,
            planOwnerId,
            cancellationToken);
    }
}
