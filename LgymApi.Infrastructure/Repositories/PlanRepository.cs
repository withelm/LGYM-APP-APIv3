using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Security.Cryptography;

namespace LgymApi.Infrastructure.Repositories;

public sealed class PlanRepository : IPlanRepository
{
    private readonly AppDbContext _dbContext;

    public PlanRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Plan?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Plans.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
    }

    public Task<Plan?> FindActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Plans.FirstOrDefaultAsync(p => p.UserId == userId && p.IsActive && !p.IsDeleted, cancellationToken);
    }

    public Task<List<Plan>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Plans.Where(p => p.UserId == userId && !p.IsDeleted).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Plan plan, CancellationToken cancellationToken = default)
    {
        await _dbContext.Plans.AddAsync(plan, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Plan plan, CancellationToken cancellationToken = default)
    {
        _dbContext.Plans.Update(plan);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActivePlanAsync(Guid userId, Guid planId, CancellationToken cancellationToken = default)
    {
        await _dbContext.Plans
            .Where(p => p.UserId == userId && p.Id != planId && !p.IsDeleted)
            .ExecuteUpdateAsync(_dbContext, p => p.IsActive, p => false, cancellationToken);

        await _dbContext.Plans
            .Where(p => p.UserId == userId && p.Id == planId && !p.IsDeleted)
            .ExecuteUpdateAsync(_dbContext, p => p.IsActive, p => true, cancellationToken);
    }

    public async Task<Plan> CopyPlanByShareCodeAsync(string shareCode, Guid userId, CancellationToken cancellationToken = default)
    {
        // 1. Find plan by ShareCode
        var planToCopy = await _dbContext.Plans
            .FirstOrDefaultAsync(p => p.ShareCode == shareCode && !p.IsDeleted, cancellationToken);

        if (planToCopy == null)
            throw new InvalidOperationException("Plan not found");

        // 2. Get all non-deleted PlanDays with their exercises
        var planDaysToCopy = await _dbContext.PlanDays
            .Include(pd => pd.Exercises)
            .ThenInclude(pde => pde.Exercise)
            .Where(pd => pd.PlanId == planToCopy.Id && !pd.IsDeleted)
            .ToListAsync(cancellationToken);

        // 3. Begin transaction for atomicity (skip for in-memory provider)
        IDbContextTransaction? transaction = null;
        if (!string.Equals(_dbContext.Database.ProviderName, "Microsoft.EntityFrameworkCore.InMemory", StringComparison.Ordinal))
        {
            transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        }

        try
        {
            // 4. Create new Plan
            var newPlan = new Plan
            {
                UserId = userId,
                Name = planToCopy.Name,
                IsActive = true,
                IsDeleted = false
            };

            await _dbContext.Plans.AddAsync(newPlan, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var copiedExercises = new Dictionary<Guid, Guid>(); // Old ExerciseId -> New ExerciseId

            // 5. Iterate through days
            foreach (var planDay in planDaysToCopy)
            {
                var newPlanDay = new PlanDay
                {
                    PlanId = newPlan.Id,
                    Name = planDay.Name,
                    IsDeleted = false
                };

                await _dbContext.PlanDays.AddAsync(newPlanDay, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);

                // 6. Iterate through exercises in the day
                foreach (var planDayExercise in planDay.Exercises)
                {
                    var exercise = planDayExercise.Exercise;
                    if (exercise == null)
                        continue;

                    Guid exerciseIdToUse;

                    // CORE LOGIC: Deep Copy for user exercises, reference for system exercises
                    if (exercise.UserId.HasValue)
                    {
                        // Check if we already copied this exercise
                        if (!copiedExercises.TryGetValue(exercise.Id, out exerciseIdToUse))
                        {
                            // Deep Copy: Create new Exercise entity for current user
                            var newExercise = new Exercise
                            {
                                Name = exercise.Name,
                                UserId = userId,
                                BodyPart = exercise.BodyPart,
                                Description = exercise.Description,
                                Image = exercise.Image,
                                IsDeleted = false
                            };

                            await _dbContext.Exercises.AddAsync(newExercise, cancellationToken);
                            await _dbContext.SaveChangesAsync(cancellationToken);

                            exerciseIdToUse = newExercise.Id;
                            copiedExercises[exercise.Id] = newExercise.Id;
                        }
                    }
                    else
                    {
                        // System exercise: Keep reference to original
                        exerciseIdToUse = exercise.Id;
                    }

                    // Create PlanDayExercise with appropriate ExerciseId
                    var newPlanDayExercise = new PlanDayExercise
                    {
                        PlanDayId = newPlanDay.Id,
                        ExerciseId = exerciseIdToUse,
                        Series = planDayExercise.Series,
                        Reps = planDayExercise.Reps
                    };

                    await _dbContext.PlanDayExercises.AddAsync(newPlanDayExercise, cancellationToken);
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            // 7. Commit transaction
            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return newPlan;
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            throw;
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public async Task<string> GenerateShareCodeAsync(Guid planId, Guid userId, CancellationToken cancellationToken = default)
    {
        var plan = await _dbContext.Plans.FirstOrDefaultAsync(p => p.Id == planId && !p.IsDeleted, cancellationToken);

        if (plan == null)
            throw new InvalidOperationException("Plan not found");

        if (plan.UserId != userId)
            throw new UnauthorizedAccessException("Only the plan owner can generate a share code");

        if (!string.IsNullOrEmpty(plan.ShareCode))
            return plan.ShareCode;

        plan.ShareCode = GenerateSecureAlphanumericCode(10);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return plan.ShareCode;
    }

    private static string GenerateSecureAlphanumericCode(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var result = new char[length];

        for (int i = 0; i < length; i++)
        {
            result[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
        }

        return new string(result);
    }
}
