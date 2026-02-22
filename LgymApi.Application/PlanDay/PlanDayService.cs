using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.PlanDay.Models;
using LgymApi.Application.Repositories;
using LgymApi.Resources;
using PlanDayEntity = LgymApi.Domain.Entities.PlanDay;
using PlanDayExerciseEntity = LgymApi.Domain.Entities.PlanDayExercise;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.PlanDay;

public sealed class PlanDayService : IPlanDayService
{
    private readonly IPlanRepository _planRepository;
    private readonly IPlanDayRepository _planDayRepository;
    private readonly IPlanDayExerciseRepository _planDayExerciseRepository;
    private readonly IExerciseRepository _exerciseRepository;
    private readonly ITrainingRepository _trainingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PlanDayService(
        IPlanRepository planRepository,
        IPlanDayRepository planDayRepository,
        IPlanDayExerciseRepository planDayExerciseRepository,
        IExerciseRepository exerciseRepository,
        ITrainingRepository trainingRepository,
        IUnitOfWork unitOfWork)
    {
        _planRepository = planRepository;
        _planDayRepository = planDayRepository;
        _planDayExerciseRepository = planDayExerciseRepository;
        _exerciseRepository = exerciseRepository;
        _trainingRepository = trainingRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task CreatePlanDayAsync(UserEntity currentUser, Guid planId, string name, IReadOnlyCollection<PlanDayExerciseInput> exercises, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || planId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var plan = await _planRepository.FindByIdAsync(planId, cancellationToken);
        if (plan == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (plan.UserId != currentUser.Id)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        if (string.IsNullOrWhiteSpace(name) || exercises.Count == 0)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var planDay = new PlanDayEntity
        {
            Id = Guid.NewGuid(),
            PlanId = plan.Id,
            Name = name,
            IsDeleted = false
        };

        await _planDayRepository.AddAsync(planDay, cancellationToken);

        var exercisesToAdd = new List<PlanDayExerciseEntity>();
        var order = 0;
        foreach (var exercise in exercises)
        {
            if (!Guid.TryParse(exercise.ExerciseId, out var exerciseId))
            {
                continue;
            }

            exercisesToAdd.Add(new PlanDayExerciseEntity
            {
                Id = Guid.NewGuid(),
                PlanDayId = planDay.Id,
                ExerciseId = exerciseId,
                Order = order++,
                Series = exercise.Series,
                Reps = exercise.Reps
            });
        }

        if (exercisesToAdd.Count > 0)
        {
            await _planDayExerciseRepository.AddRangeAsync(exercisesToAdd, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdatePlanDayAsync(UserEntity currentUser, string planDayId, string name, IReadOnlyCollection<PlanDayExerciseInput> exercises, CancellationToken cancellationToken = default)
    {
        if (currentUser == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (string.IsNullOrWhiteSpace(name) || exercises.Count == 0)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        if (!Guid.TryParse(planDayId, out var planDayGuid))
        {
            throw AppException.BadRequest(Messages.DidntFind);
        }

        var planDay = await _planDayRepository.FindByIdAsync(planDayGuid, cancellationToken);
        if (planDay == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var plan = await _planRepository.FindByIdAsync(planDay.PlanId, cancellationToken);
        if (plan == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (plan.UserId != currentUser.Id)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        planDay.Name = name;
        await _planDayRepository.UpdateAsync(planDay, cancellationToken);

        await _planDayExerciseRepository.RemoveByPlanDayIdAsync(planDay.Id, cancellationToken);

        var exercisesToAdd = new List<PlanDayExerciseEntity>();
        var order = 0;
        foreach (var exercise in exercises)
        {
            if (!Guid.TryParse(exercise.ExerciseId, out var exerciseId))
            {
                continue;
            }

            exercisesToAdd.Add(new PlanDayExerciseEntity
            {
                Id = Guid.NewGuid(),
                PlanDayId = planDay.Id,
                ExerciseId = exerciseId,
                Order = order++,
                Series = exercise.Series,
                Reps = exercise.Reps
            });
        }

        if (exercisesToAdd.Count > 0)
        {
            await _planDayExerciseRepository.AddRangeAsync(exercisesToAdd, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<PlanDayDetailsContext> GetPlanDayAsync(UserEntity currentUser, Guid planDayId, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || planDayId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var planDay = await _planDayRepository.FindByIdAsync(planDayId, cancellationToken);
        if (planDay == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var plan = await _planRepository.FindByIdAsync(planDay.PlanId, cancellationToken);
        if (plan == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (plan.UserId != currentUser.Id)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        var exercises = await _planDayExerciseRepository.GetByPlanDayIdAsync(planDay.Id, cancellationToken);
        var exerciseIds = exercises.Select(e => e.ExerciseId).Distinct().ToList();
        var exerciseList = await _exerciseRepository.GetByIdsAsync(exerciseIds, cancellationToken);
        var exerciseMap = exerciseList.ToDictionary(e => e.Id, e => e);

        return new PlanDayDetailsContext
        {
            PlanDay = planDay,
            Exercises = exercises,
            ExerciseMap = exerciseMap
        };
    }

    public async Task<PlanDaysContext> GetPlanDaysAsync(UserEntity currentUser, Guid planId, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || planId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var plan = await _planRepository.FindByIdAsync(planId, cancellationToken);
        if (plan == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (plan.UserId != currentUser.Id)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        var planDays = await _planDayRepository.GetByPlanIdAsync(plan.Id, cancellationToken);
        if (planDays.Count == 0)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var planDayIds = planDays.Select(pd => pd.Id).ToList();
        var planDayExercises = await _planDayExerciseRepository.GetByPlanDayIdsAsync(planDayIds, cancellationToken);

        var exerciseIds = planDayExercises.Select(e => e.ExerciseId).Distinct().ToList();
        var exerciseList = await _exerciseRepository.GetByIdsAsync(exerciseIds, cancellationToken);
        var exerciseMap = exerciseList.ToDictionary(e => e.Id, e => e);

        return new PlanDaysContext
        {
            PlanDays = planDays,
            PlanDayExercises = planDayExercises,
            ExerciseMap = exerciseMap
        };
    }

    public async Task<List<PlanDayEntity>> GetPlanDaysTypesAsync(UserEntity currentUser, Guid routeUserId, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || routeUserId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (currentUser.Id != routeUserId)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        var plan = await _planRepository.FindActiveByUserIdAsync(currentUser.Id, cancellationToken);
        if (plan == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return await _planDayRepository.GetByPlanIdAsync(plan.Id, cancellationToken);
    }

    public async Task DeletePlanDayAsync(UserEntity currentUser, Guid planDayId, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || planDayId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var planDay = await _planDayRepository.FindByIdAsync(planDayId, cancellationToken);
        if (planDay == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var plan = await _planRepository.FindByIdAsync(planDay.PlanId, cancellationToken);
        if (plan == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (plan.UserId != currentUser.Id)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        await _planDayRepository.MarkDeletedAsync(planDay.Id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<PlanDaysInfoContext> GetPlanDaysInfoAsync(UserEntity currentUser, Guid planId, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || planId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var plan = await _planRepository.FindByIdAsync(planId, cancellationToken);
        if (plan == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (plan.UserId != currentUser.Id)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        var planDays = await _planDayRepository.GetByPlanIdAsync(plan.Id, cancellationToken);

        var planDayIds = planDays.Select(pd => pd.Id).ToList();
        var planDayExercises = await _planDayExerciseRepository.GetByPlanDayIdsAsync(planDayIds, cancellationToken);

        var trainings = await _trainingRepository.GetByPlanDayIdsAsync(planDayIds, cancellationToken);
        var lastTrainingMap = trainings
            .GroupBy(t => t.TypePlanDayId)
            .ToDictionary(g => g.Key, g => (DateTime?)g.Max(t => t.CreatedAt).UtcDateTime);

        return new PlanDaysInfoContext
        {
            PlanDays = planDays,
            PlanDayExercises = planDayExercises,
            LastTrainingMap = lastTrainingMap
        };
    }
}
