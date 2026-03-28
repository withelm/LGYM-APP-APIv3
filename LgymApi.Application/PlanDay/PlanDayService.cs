using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.PlanDay.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.ValueObjects;
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

    public PlanDayService(IPlanDayServiceDependencies dependencies)
    {
        _planRepository = dependencies.PlanRepository;
        _planDayRepository = dependencies.PlanDayRepository;
        _planDayExerciseRepository = dependencies.PlanDayExerciseRepository;
        _exerciseRepository = dependencies.ExerciseRepository;
        _trainingRepository = dependencies.TrainingRepository;
        _unitOfWork = dependencies.UnitOfWork;
    }

    public async Task CreatePlanDayAsync(UserEntity currentUser, Id<LgymApi.Domain.Entities.Plan> planId, string name, IReadOnlyCollection<PlanDayExerciseInput> exercises, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || planId.IsEmpty)
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
            Id = Id<PlanDayEntity>.New(),
            PlanId = plan.Id,
            Name = name,
            IsDeleted = false
        };

        await _planDayRepository.AddAsync(planDay, cancellationToken);

        var exercisesToAdd = new List<PlanDayExerciseEntity>();
        var order = 0;
        foreach (var exercise in exercises)
        {
            if (exercise.ExerciseId.IsEmpty)
            {
                continue;
            }

            exercisesToAdd.Add(new PlanDayExerciseEntity
            {
                Id = Id<PlanDayExerciseEntity>.New(),
                PlanDayId = planDay.Id,
                ExerciseId = exercise.ExerciseId,
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

    public async Task UpdatePlanDayAsync(UserEntity currentUser, Id<PlanDayEntity> planDayId, string name, IReadOnlyCollection<PlanDayExerciseInput> exercises, CancellationToken cancellationToken = default)
    {
        if (currentUser == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (string.IsNullOrWhiteSpace(name) || exercises.Count == 0)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        if (planDayId.IsEmpty)
        {
            throw AppException.BadRequest(Messages.DidntFind);
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

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            planDay.Name = name;
            await _planDayRepository.UpdateAsync(planDay, cancellationToken);

            await _planDayExerciseRepository.RemoveByPlanDayIdAsync(planDay.Id, cancellationToken);

            var exercisesToAdd = new List<PlanDayExerciseEntity>();
            var order = 0;
            foreach (var exercise in exercises)
            {
                if (exercise.ExerciseId.IsEmpty)
                {
                    continue;
                }

                exercisesToAdd.Add(new PlanDayExerciseEntity
                {
                    Id = Id<PlanDayExerciseEntity>.New(),
                    PlanDayId = planDay.Id,
                    ExerciseId = exercise.ExerciseId,
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
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<PlanDayDetailsContext> GetPlanDayAsync(UserEntity currentUser, Id<PlanDayEntity> planDayId, IReadOnlyList<string> cultures, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || planDayId.IsEmpty)
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
        var globalExerciseIds = exerciseList
            .Where(e => e.UserId == null)
            .Select(e => e.Id)
            .ToList();
        var translations = await _exerciseRepository.GetTranslationsAsync(globalExerciseIds, cultures, cancellationToken);

        return new PlanDayDetailsContext
        {
            PlanDay = planDay,
            Exercises = exercises,
            ExerciseMap = exerciseMap,
            Translations = translations
        };
    }

    public async Task<PlanDaysContext> GetPlanDaysAsync(UserEntity currentUser, Id<LgymApi.Domain.Entities.Plan> planId, IReadOnlyList<string> cultures, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || planId.IsEmpty)
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
        var globalExerciseIds = exerciseList
            .Where(e => e.UserId == null)
            .Select(e => e.Id)
            .ToList();
        var translations = await _exerciseRepository.GetTranslationsAsync(globalExerciseIds, cultures, cancellationToken);

        return new PlanDaysContext
        {
            PlanDays = planDays,
            PlanDayExercises = planDayExercises,
            ExerciseMap = exerciseMap,
            Translations = translations
        };
    }

    public async Task<List<PlanDayEntity>> GetPlanDaysTypesAsync(UserEntity currentUser, Id<UserEntity> routeUserId, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || routeUserId.IsEmpty)
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

    public async Task DeletePlanDayAsync(UserEntity currentUser, Id<PlanDayEntity> planDayId, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || planDayId.IsEmpty)
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

    public async Task<PlanDaysInfoContext> GetPlanDaysInfoAsync(UserEntity currentUser, Id<LgymApi.Domain.Entities.Plan> planId, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || planId.IsEmpty)
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
