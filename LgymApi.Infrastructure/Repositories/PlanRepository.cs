using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace LgymApi.Infrastructure.Repositories;

public sealed class PlanRepository : IPlanRepository
{
    /// <summary>
    /// Length of generated share codes.
    /// </summary>
    private const int ShareCodeLength = 10;

    /// <summary>
    /// Upper bound for collision retry attempts.
    /// </summary>
    private const int ShareCodeGenerationMaxAttempts = 10;
    private const string ShareCodeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private static readonly HashSet<char> ShareCodeAllowedCharacters =
    [
        ..ShareCodeAlphabet
    ];

    private readonly AppDbContext _dbContext;
    private readonly Func<int, string> _shareCodeGenerator;

    public PlanRepository(AppDbContext dbContext, Func<int, string>? shareCodeGenerator = null)
    {
        _dbContext = dbContext;
        _shareCodeGenerator = shareCodeGenerator ?? GenerateSecureAlphanumericCode;
    }

    public Task<Plan?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Plans.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
    }

    public Task<Plan?> FindActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Plans.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId && p.IsActive && !p.IsDeleted, cancellationToken);
    }

    public Task<Plan?> FindLastActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Plans
            .AsNoTracking()
            .Where(p => p.UserId == userId && !p.IsActive && !p.IsDeleted)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<List<Plan>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Plans.AsNoTracking().Where(p => p.UserId == userId && !p.IsDeleted).ToListAsync(cancellationToken);
    }

    public Task AddAsync(Plan plan, CancellationToken cancellationToken = default)
    {
        return _dbContext.Plans.AddAsync(plan, cancellationToken).AsTask();
    }

    public Task UpdateAsync(Plan plan, CancellationToken cancellationToken = default)
    {
        _dbContext.Plans.Update(plan);
        return Task.CompletedTask;
    }

    public async Task SetActivePlanAsync(Guid userId, Guid planId, CancellationToken cancellationToken = default)
    {
        await _dbContext.Plans
            .Where(p => p.UserId == userId && p.Id != planId && !p.IsDeleted)
            .StageUpdateAsync(_dbContext, p => p.IsActive, p => false, cancellationToken);

        await _dbContext.Plans
            .Where(p => p.UserId == userId && p.Id == planId && !p.IsDeleted)
            .StageUpdateAsync(_dbContext, p => p.IsActive, p => true, cancellationToken);
    }

    public Task ClearActivePlansAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Plans
            .Where(p => p.UserId == userId && !p.IsDeleted)
            .StageUpdateAsync(_dbContext, p => p.IsActive, p => false, cancellationToken);
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

        // 3. Build copied graph in current unit of work.
        var newPlan = new Plan
        {
            UserId = userId,
            Name = planToCopy.Name,
            IsActive = true,
            IsDeleted = false
        };

        await _dbContext.Plans.AddAsync(newPlan, cancellationToken);

        var copiedExercises = new Dictionary<Guid, Exercise>(); // Old ExerciseId -> New Exercise

        // 4. Iterate through days
        foreach (var planDay in planDaysToCopy)
        {
            var newPlanDay = new PlanDay
            {
                Plan = newPlan,
                Name = planDay.Name,
                IsDeleted = false
            };

            await _dbContext.PlanDays.AddAsync(newPlanDay, cancellationToken);

            // 5. Iterate through exercises in the day
            foreach (var planDayExercise in planDay.Exercises.OrderBy(e => e.Order).ThenBy(e => e.Id))
            {
                var exercise = planDayExercise.Exercise;
                if (exercise == null)
                {
                    continue;
                }

                Exercise exerciseToUse;

                // CORE LOGIC: Deep Copy for user exercises, reference for system exercises
                if (exercise.UserId.HasValue)
                {
                    // Check if we already copied this exercise
                    if (!copiedExercises.TryGetValue(exercise.Id, out var copiedExercise))
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
                        copiedExercises[exercise.Id] = newExercise;
                        exerciseToUse = newExercise;
                    }
                    else
                    {
                        exerciseToUse = copiedExercise;
                    }
                }
                else
                {
                    // System exercise: Keep reference to original
                    exerciseToUse = exercise;
                }

                var newPlanDayExercise = new PlanDayExercise
                {
                    PlanDay = newPlanDay,
                    Exercise = exerciseToUse,
                    Order = planDayExercise.Order,
                    Series = planDayExercise.Series,
                    Reps = planDayExercise.Reps
                };

                await _dbContext.PlanDayExercises.AddAsync(newPlanDayExercise, cancellationToken);
            }
        }

        return newPlan;
    }

    public async Task<string> GenerateShareCodeAsync(Guid planId, Guid userId, CancellationToken cancellationToken = default)
    {
        var plan = await _dbContext.Plans.FirstOrDefaultAsync(p => p.Id == planId && !p.IsDeleted, cancellationToken);

        if (plan == null)
            throw new KeyNotFoundException("Plan not found");

        if (plan.UserId != userId)
            throw new UnauthorizedAccessException("Only the plan owner can generate a share code");

        if (!string.IsNullOrEmpty(plan.ShareCode))
        {
            var isCurrentCodeTaken = await IsShareCodeTakenAsync(plan.ShareCode, plan.Id, cancellationToken);
            if (!isCurrentCodeTaken)
            {
                return plan.ShareCode;
            }

            plan.ShareCode = null;
        }

        for (var attempt = 0; attempt < ShareCodeGenerationMaxAttempts; attempt++)
        {
            var candidateCode = _shareCodeGenerator(ShareCodeLength);
            if (!IsValidShareCode(candidateCode))
            {
                continue;
            }

            var isTaken = await IsShareCodeTakenAsync(candidateCode, plan.Id, cancellationToken);
            if (isTaken)
            {
                continue;
            }

            plan.ShareCode = candidateCode;
            return plan.ShareCode;
        }

        throw new InvalidOperationException("Unable to generate unique share code");
    }

    private Task<bool> IsShareCodeTakenAsync(string shareCode, Guid currentPlanId, CancellationToken cancellationToken)
    {
        return _dbContext.Plans.AnyAsync(
            p => p.Id != currentPlanId && p.ShareCode == shareCode && !p.IsDeleted,
            cancellationToken);
    }

    private static bool IsValidShareCode(string? shareCode)
    {
        if (string.IsNullOrWhiteSpace(shareCode) || shareCode.Length != ShareCodeLength)
        {
            return false;
        }

        return shareCode.All(ShareCodeAllowedCharacters.Contains);
    }

    private static string GenerateSecureAlphanumericCode(int length)
    {
        var result = new char[length];

        for (int i = 0; i < length; i++)
        {
            result[i] = ShareCodeAlphabet[RandomNumberGenerator.GetInt32(ShareCodeAlphabet.Length)];
        }

        return new string(result);
    }

}
