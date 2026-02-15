using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.PlanDay.Models;
using LgymApi.Application.Repositories;
using LgymApi.Resources;
using PlanDayEntity = LgymApi.Domain.Entities.PlanDay;
using PlanDayExerciseEntity = LgymApi.Domain.Entities.PlanDayExercise;

namespace LgymApi.Application.Features.PlanDay;

public sealed class PlanDayService : IPlanDayService
{
    private readonly IPlanRepository _planRepository;
    private readonly IPlanDayRepository _planDayRepository;
    private readonly IPlanDayExerciseRepository _planDayExerciseRepository;
    private readonly IExerciseRepository _exerciseRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITrainingRepository _trainingRepository;

    public PlanDayService(
        IPlanRepository planRepository,
        IPlanDayRepository planDayRepository,
        IPlanDayExerciseRepository planDayExerciseRepository,
        IExerciseRepository exerciseRepository,
        IUserRepository userRepository,
        ITrainingRepository trainingRepository)
    {
        _planRepository = planRepository;
        _planDayRepository = planDayRepository;
        _planDayExerciseRepository = planDayExerciseRepository;
        _exerciseRepository = exerciseRepository;
        _userRepository = userRepository;
        _trainingRepository = trainingRepository;
    }

    public async Task CreatePlanDayAsync(Guid planId, string name, IReadOnlyCollection<PlanDayExerciseInput> exercises)
    {
        if (planId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var plan = await _planRepository.FindByIdAsync(planId);
        if (plan == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
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

        await _planDayRepository.AddAsync(planDay);

        var exercisesToAdd = new List<PlanDayExerciseEntity>();
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
                Series = exercise.Series,
                Reps = exercise.Reps
            });
        }

        if (exercisesToAdd.Count > 0)
        {
            await _planDayExerciseRepository.AddRangeAsync(exercisesToAdd);
        }
    }

    public async Task UpdatePlanDayAsync(string planDayId, string name, IReadOnlyCollection<PlanDayExerciseInput> exercises)
    {
        if (string.IsNullOrWhiteSpace(name) || exercises.Count == 0)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        if (!Guid.TryParse(planDayId, out var planDayGuid))
        {
            throw AppException.BadRequest(Messages.DidntFind);
        }

        var planDay = await _planDayRepository.FindByIdAsync(planDayGuid);
        if (planDay == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        planDay.Name = name;
        await _planDayRepository.UpdateAsync(planDay);

        await _planDayExerciseRepository.RemoveByPlanDayIdAsync(planDay.Id);

        var exercisesToAdd = new List<PlanDayExerciseEntity>();
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
                Series = exercise.Series,
                Reps = exercise.Reps
            });
        }

        if (exercisesToAdd.Count > 0)
        {
            await _planDayExerciseRepository.AddRangeAsync(exercisesToAdd);
        }
    }

    public async Task<PlanDayDetailsContext> GetPlanDayAsync(Guid planDayId)
    {
        if (planDayId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var planDay = await _planDayRepository.FindByIdAsync(planDayId);
        if (planDay == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var exercises = await _planDayExerciseRepository.GetByPlanDayIdAsync(planDay.Id);
        var exerciseIds = exercises.Select(e => e.ExerciseId).Distinct().ToList();
        var exerciseList = await _exerciseRepository.GetByIdsAsync(exerciseIds);
        var exerciseMap = exerciseList.ToDictionary(e => e.Id, e => e);

        return new PlanDayDetailsContext
        {
            PlanDay = planDay,
            Exercises = exercises,
            ExerciseMap = exerciseMap
        };
    }

    public async Task<PlanDaysContext> GetPlanDaysAsync(Guid planId)
    {
        if (planId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var plan = await _planRepository.FindByIdAsync(planId);
        if (plan == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var planDays = await _planDayRepository.GetByPlanIdAsync(plan.Id);
        if (planDays.Count == 0)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var planDayIds = planDays.Select(pd => pd.Id).ToList();
        var planDayExercises = await _planDayExerciseRepository.GetByPlanDayIdsAsync(planDayIds);

        var exerciseIds = planDayExercises.Select(e => e.ExerciseId).Distinct().ToList();
        var exerciseList = await _exerciseRepository.GetByIdsAsync(exerciseIds);
        var exerciseMap = exerciseList.ToDictionary(e => e.Id, e => e);

        return new PlanDaysContext
        {
            PlanDays = planDays,
            PlanDayExercises = planDayExercises,
            ExerciseMap = exerciseMap
        };
    }

    public async Task<List<PlanDayEntity>> GetPlanDaysTypesAsync(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var user = await _userRepository.FindByIdAsync(userId);
        if (user == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var plan = await _planRepository.FindActiveByUserIdAsync(user.Id);
        if (plan == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return await _planDayRepository.GetByPlanIdAsync(plan.Id);
    }

    public async Task DeletePlanDayAsync(Guid planDayId)
    {
        if (planDayId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var planDay = await _planDayRepository.FindByIdAsync(planDayId);
        if (planDay == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        await _planDayRepository.MarkDeletedAsync(planDay.Id);
    }

    public async Task<PlanDaysInfoContext> GetPlanDaysInfoAsync(Guid planId)
    {
        if (planId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var plan = await _planRepository.FindByIdAsync(planId);
        if (plan == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var planDays = await _planDayRepository.GetByPlanIdAsync(plan.Id);
        if (planDays.Count == 0)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var planDayIds = planDays.Select(pd => pd.Id).ToList();
        var planDayExercises = await _planDayExerciseRepository.GetByPlanDayIdsAsync(planDayIds);

        var trainings = await _trainingRepository.GetByPlanDayIdsAsync(planDayIds);
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
