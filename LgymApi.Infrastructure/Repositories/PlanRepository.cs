using LgymApi.Application.Repositories;
using LgymApi.Application.TrainingPlanning.Plan.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace LgymApi.Infrastructure.Repositories;

public sealed partial class PlanRepository : IPlanRepository
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

    public Task<Plan?> FindByIdAsync(Id<Plan> id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Plans.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
    }

    public Task<Plan?> FindActiveByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Plans.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId && p.IsActive && !p.IsDeleted, cancellationToken);
    }

    public Task<PlanReadModel?> FindActiveReadModelByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Plans
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.IsActive && !p.IsDeleted)
            .Select(p => new PlanReadModel(p.Id, p.UserId, p.Name, p.IsActive, p.ShareCode))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Plan?> FindLastActiveByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Plans
            .AsNoTracking()
            .Where(p => p.UserId == userId && !p.IsActive && !p.IsDeleted)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<List<Plan>> GetByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Plans.AsNoTracking().Where(p => p.UserId == userId && !p.IsDeleted).ToListAsync(cancellationToken);
    }

    public Task<List<PlanReadModel>> GetReadModelsByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Plans
            .AsNoTracking()
            .Where(p => p.UserId == userId && !p.IsDeleted)
            .Select(p => new PlanReadModel(p.Id, p.UserId, p.Name, p.IsActive, p.ShareCode))
            .ToListAsync(cancellationToken);
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

    public async Task SetActivePlanAsync(Id<User> userId, Id<Plan> planId, CancellationToken cancellationToken = default)
    {
        await _dbContext.Plans
            .Where(p => p.UserId == userId && p.Id != planId && !p.IsDeleted)
            .StageUpdateAsync(_dbContext, p => p.IsActive, p => false, cancellationToken);

        await _dbContext.Plans
            .Where(p => p.UserId == userId && p.Id == planId && !p.IsDeleted)
            .StageUpdateAsync(_dbContext, p => p.IsActive, p => true, cancellationToken);
    }

    public Task ClearActivePlansAsync(Id<User> userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Plans
            .Where(p => p.UserId == userId && !p.IsDeleted)
            .StageUpdateAsync(_dbContext, p => p.IsActive, p => false, cancellationToken);
    }

    public async Task<Plan> ClonePlanAsync(Id<Plan> sourcePlanId, Id<User> userId, bool isActive = true, CancellationToken cancellationToken = default)
    {
        var planToCopy = await _dbContext.Plans
            .FirstOrDefaultAsync(p => p.Id == sourcePlanId && !p.IsDeleted, cancellationToken);

        if (planToCopy == null)
        {
            throw new InvalidOperationException("Plan not found");
        }

        return await ClonePlanGraphAsync(planToCopy, userId, isActive, cancellationToken);
    }

    public async Task<Plan> CopyPlanByShareCodeAsync(string shareCode, Id<User> userId, CancellationToken cancellationToken = default)
    {
        var planToCopy = await _dbContext.Plans
            .FirstOrDefaultAsync(p => p.ShareCode == shareCode && !p.IsDeleted, cancellationToken);

        if (planToCopy == null)
            throw new InvalidOperationException("Plan not found");

        return await ClonePlanGraphAsync(planToCopy, userId, isActive: true, cancellationToken);
    }

    public async Task<string> GenerateShareCodeAsync(Id<Plan> planId, Id<User> userId, CancellationToken cancellationToken = default)
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

    private Task<bool> IsShareCodeTakenAsync(string shareCode, Id<Plan> currentPlanId, CancellationToken cancellationToken)
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
