using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed partial class PlanRepository
{
    private async Task<Plan> ClonePlanGraphAsync(Plan planToCopy, Id<User> userId, bool isActive, CancellationToken cancellationToken)
    {
        var planDaysToCopy = await LoadPlanDaysAsync(planToCopy.Id, cancellationToken);
        var newPlan = await CreateNewPlanAsync(planToCopy, userId, isActive, cancellationToken);
        var copiedExercises = new Dictionary<Id<Exercise>, Exercise>();

        foreach (var planDay in planDaysToCopy)
        {
            await ProcessPlanDayAsync(planDay, newPlan.Id, copiedExercises, userId, cancellationToken);
        }

        return newPlan;
    }

    private Task<List<PlanDay>> LoadPlanDaysAsync(Id<Plan> planId, CancellationToken cancellationToken)
        => _dbContext.PlanDays
            .Include(pd => pd.Exercises)
            .ThenInclude(pde => pde.Exercise)
            .Where(pd => pd.PlanId == planId && !pd.IsDeleted)
            .ToListAsync(cancellationToken);

    private async Task<Plan> CreateNewPlanAsync(Plan sourcePlan, Id<User> userId, bool isActive, CancellationToken cancellationToken)
    {
        var newPlan = new Plan { Id = Id<Plan>.New(), UserId = userId, Name = sourcePlan.Name, IsActive = isActive, IsDeleted = false };
        await _dbContext.Plans.AddAsync(newPlan, cancellationToken);
        return newPlan;
    }

    private async Task ProcessPlanDayAsync(PlanDay sourcePlanDay, Id<Plan> newPlanId, Dictionary<Id<Exercise>, Exercise> copiedExercises, Id<User> targetUserId, CancellationToken cancellationToken)
    {
        var newPlanDay = new PlanDay { Id = Id<PlanDay>.New(), PlanId = newPlanId, Name = sourcePlanDay.Name, IsDeleted = false };
        await _dbContext.PlanDays.AddAsync(newPlanDay, cancellationToken);

        foreach (var planDayExercise in sourcePlanDay.Exercises.OrderBy(e => e.Order).ThenBy(e => e.Id))
        {
            var exerciseToUse = await ResolveExerciseForCopiedPlanAsync(planDayExercise.Exercise, copiedExercises, targetUserId, cancellationToken);
            if (exerciseToUse != null)
            {
                await AddCopiedPlanDayExerciseAsync(newPlanDay.Id, planDayExercise, exerciseToUse.Id, cancellationToken);
            }
        }
    }

    private async Task<Exercise?> ResolveExerciseForCopiedPlanAsync(Exercise? sourceExercise, Dictionary<Id<Exercise>, Exercise> copiedExercises, Id<User> targetUserId, CancellationToken cancellationToken)
    {
        if (sourceExercise == null) return null;
        return !sourceExercise.UserId.HasValue
            ? sourceExercise
            : await CopyUserExerciseIfNeededAsync(sourceExercise, copiedExercises, targetUserId, cancellationToken);
    }

    private async Task<Exercise> CopyUserExerciseIfNeededAsync(Exercise sourceExercise, Dictionary<Id<Exercise>, Exercise> copiedExercises, Id<User> targetUserId, CancellationToken cancellationToken)
    {
        if (copiedExercises.TryGetValue(sourceExercise.Id, out var copiedExercise)) return copiedExercise;

        var newExercise = new Exercise { Id = Id<Exercise>.New(), Name = sourceExercise.Name, UserId = targetUserId, BodyPart = sourceExercise.BodyPart, Description = sourceExercise.Description, Image = sourceExercise.Image, IsDeleted = false };
        await _dbContext.Exercises.AddAsync(newExercise, cancellationToken);
        copiedExercises[sourceExercise.Id] = newExercise;
        return newExercise;
    }

    private Task AddCopiedPlanDayExerciseAsync(Id<PlanDay> newPlanDayId, PlanDayExercise sourcePlanDayExercise, Id<Exercise> exerciseId, CancellationToken cancellationToken)
        => _dbContext.PlanDayExercises.AddAsync(new PlanDayExercise
        {
            Id = Id<PlanDayExercise>.New(),
            PlanDayId = newPlanDayId,
            ExerciseId = exerciseId,
            Order = sourcePlanDayExercise.Order,
            Series = sourcePlanDayExercise.Series,
            Reps = sourcePlanDayExercise.Reps
        }, cancellationToken).AsTask();
}
