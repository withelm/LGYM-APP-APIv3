using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
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

    public async Task<Result<Unit, AppError>> CreatePlanDayAsync(UserEntity currentUser, Id<LgymApi.Domain.Entities.Plan> planId, string name, IReadOnlyCollection<PlanDayExerciseInput> exercises, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || planId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new PlanDayNotFoundError(Messages.DidntFind));
        }

        var plan = await _planRepository.FindByIdAsync(planId, cancellationToken);
        if (plan == null)
        {
            return Result<Unit, AppError>.Failure(new PlanDayNotFoundError(Messages.DidntFind));
        }

        if (plan.UserId != currentUser.Id)
        {
            return Result<Unit, AppError>.Failure(new PlanDayForbiddenError(Messages.Forbidden));
        }

        if (string.IsNullOrWhiteSpace(name) || exercises.Count == 0)
        {
            return Result<Unit, AppError>.Failure(new InvalidPlanDayError(Messages.FieldRequired));
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
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> UpdatePlanDayAsync(UserEntity currentUser, Id<PlanDayEntity> planDayId, string name, IReadOnlyCollection<PlanDayExerciseInput> exercises, CancellationToken cancellationToken = default)
    {
        if (currentUser == null)
        {
            return Result<Unit, AppError>.Failure(new PlanDayNotFoundError(Messages.DidntFind));
        }

        if (string.IsNullOrWhiteSpace(name) || exercises.Count == 0)
        {
            return Result<Unit, AppError>.Failure(new InvalidPlanDayError(Messages.FieldRequired));
        }

        if (planDayId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidPlanDayError(Messages.DidntFind));
        }

        var planDay = await _planDayRepository.FindByIdAsync(planDayId, cancellationToken);
        if (planDay == null)
        {
            return Result<Unit, AppError>.Failure(new PlanDayNotFoundError(Messages.DidntFind));
        }

        var plan = await _planRepository.FindByIdAsync(planDay.PlanId, cancellationToken);
        if (plan == null)
        {
            return Result<Unit, AppError>.Failure(new PlanDayNotFoundError(Messages.DidntFind));
        }

        if (plan.UserId != currentUser.Id)
        {
            return Result<Unit, AppError>.Failure(new PlanDayForbiddenError(Messages.Forbidden));
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
            return Result<Unit, AppError>.Success(Unit.Value);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<Result<PlanDayDetailsContext, AppError>> GetPlanDayAsync(UserEntity currentUser, Id<PlanDayEntity> planDayId, IReadOnlyList<string> cultures, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || planDayId.IsEmpty)
        {
            return Result<PlanDayDetailsContext, AppError>.Failure(new PlanDayNotFoundError(Messages.DidntFind));
        }

        var planDay = await _planDayRepository.FindByIdAsync(planDayId, cancellationToken);
        if (planDay == null)
        {
            return Result<PlanDayDetailsContext, AppError>.Failure(new PlanDayNotFoundError(Messages.DidntFind));
        }

        var plan = await _planRepository.FindByIdAsync(planDay.PlanId, cancellationToken);
        if (plan == null)
        {
            return Result<PlanDayDetailsContext, AppError>.Failure(new PlanDayNotFoundError(Messages.DidntFind));
        }

        if (plan.UserId != currentUser.Id)
        {
            return Result<PlanDayDetailsContext, AppError>.Failure(new PlanDayForbiddenError(Messages.Forbidden));
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

        return Result<PlanDayDetailsContext, AppError>.Success(new PlanDayDetailsContext
        {
            PlanDay = planDay,
            Exercises = exercises,
            ExerciseMap = exerciseMap,
            Translations = translations
        });
    }

    public async Task<Result<PlanDaysContext, AppError>> GetPlanDaysAsync(UserEntity currentUser, Id<LgymApi.Domain.Entities.Plan> planId, IReadOnlyList<string> cultures, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || planId.IsEmpty)
        {
            return Result<PlanDaysContext, AppError>.Failure(new PlanDayNotFoundError(Messages.DidntFind));
        }

        var plan = await _planRepository.FindByIdAsync(planId, cancellationToken);
        if (plan == null)
        {
            return Result<PlanDaysContext, AppError>.Failure(new PlanDayNotFoundError(Messages.DidntFind));
        }

        if (plan.UserId != currentUser.Id)
        {
            return Result<PlanDaysContext, AppError>.Failure(new PlanDayForbiddenError(Messages.Forbidden));
        }

        var planDays = await _planDayRepository.GetByPlanIdAsync(plan.Id, cancellationToken);
        if (planDays.Count == 0)
        {
            return Result<PlanDaysContext, AppError>.Failure(new PlanDayNotFoundError(Messages.DidntFind));
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

        return Result<PlanDaysContext, AppError>.Success(new PlanDaysContext
        {
            PlanDays = planDays,
            PlanDayExercises = planDayExercises,
            ExerciseMap = exerciseMap,
            Translations = translations
        });
    }

    public async Task<Result<List<PlanDayEntity>, AppError>> GetPlanDaysTypesAsync(UserEntity currentUser, Id<UserEntity> routeUserId, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || routeUserId.IsEmpty)
        {
            return Result<List<PlanDayEntity>, AppError>.Failure(new PlanDayNotFoundError(Messages.DidntFind));
        }

        if (currentUser.Id != routeUserId)
        {
            return Result<List<PlanDayEntity>, AppError>.Failure(new PlanDayForbiddenError(Messages.Forbidden));
        }

        var plan = await _planRepository.FindActiveByUserIdAsync(currentUser.Id, cancellationToken);
        if (plan == null)
        {
            return Result<List<PlanDayEntity>, AppError>.Failure(new PlanDayNotFoundError(Messages.DidntFind));
        }

        var planDays = await _planDayRepository.GetByPlanIdAsync(plan.Id, cancellationToken);
        return Result<List<PlanDayEntity>, AppError>.Success(planDays);
    }

    public async Task<Result<Unit, AppError>> DeletePlanDayAsync(UserEntity currentUser, Id<PlanDayEntity> planDayId, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || planDayId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new PlanDayNotFoundError(Messages.DidntFind));
        }

        var planDay = await _planDayRepository.FindByIdAsync(planDayId, cancellationToken);
        if (planDay == null)
        {
            return Result<Unit, AppError>.Failure(new PlanDayNotFoundError(Messages.DidntFind));
        }

        var plan = await _planRepository.FindByIdAsync(planDay.PlanId, cancellationToken);
        if (plan == null)
        {
            return Result<Unit, AppError>.Failure(new PlanDayNotFoundError(Messages.DidntFind));
        }

        if (plan.UserId != currentUser.Id)
        {
            return Result<Unit, AppError>.Failure(new PlanDayForbiddenError(Messages.Forbidden));
        }

        await _planDayRepository.MarkDeletedAsync(planDay.Id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<PlanDaysInfoContext, AppError>> GetPlanDaysInfoAsync(UserEntity currentUser, Id<LgymApi.Domain.Entities.Plan> planId, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || planId.IsEmpty)
        {
            return Result<PlanDaysInfoContext, AppError>.Failure(new PlanDayNotFoundError(Messages.DidntFind));
        }

        var plan = await _planRepository.FindByIdAsync(planId, cancellationToken);
        if (plan == null)
        {
            return Result<PlanDaysInfoContext, AppError>.Failure(new PlanDayNotFoundError(Messages.DidntFind));
        }

        if (plan.UserId != currentUser.Id)
        {
            return Result<PlanDaysInfoContext, AppError>.Failure(new PlanDayForbiddenError(Messages.Forbidden));
        }

        var planDays = await _planDayRepository.GetByPlanIdAsync(plan.Id, cancellationToken);

        var planDayIds = planDays.Select(pd => pd.Id).ToList();
        var planDayExercises = await _planDayExerciseRepository.GetByPlanDayIdsAsync(planDayIds, cancellationToken);

        var trainings = await _trainingRepository.GetByPlanDayIdsAsync(planDayIds, cancellationToken);
        var lastTrainingMap = trainings
            .GroupBy(t => t.TypePlanDayId)
            .ToDictionary(g => g.Key, g => (DateTime?)g.Max(t => t.CreatedAt).UtcDateTime);

        return Result<PlanDaysInfoContext, AppError>.Success(new PlanDaysInfoContext
        {
            PlanDays = planDays,
            PlanDayExercises = planDayExercises,
            LastTrainingMap = lastTrainingMap
        });
    }
}
