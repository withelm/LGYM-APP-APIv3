using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class MeasurementRepository : IMeasurementRepository
{
    private readonly AppDbContext _dbContext;

    public MeasurementRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(Measurement measurement, CancellationToken cancellationToken = default)
    {
        return _dbContext.Measurements.AddAsync(measurement, cancellationToken).AsTask();
    }

    public Task<Measurement?> FindByIdAsync(Id<Measurement> id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Measurements.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public Task<List<Measurement>> GetByUserAsync(Id<User> userId, BodyParts? bodyPart, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Measurements.AsNoTracking().Where(m => m.UserId == userId).AsQueryable();
        if (bodyPart.HasValue)
        {
            query = query.Where(m => m.BodyPart == bodyPart.Value);
        }

        return query.ToListAsync(cancellationToken);
    }

    public async Task<HashSet<BodyParts>> GetExistingBodyPartsByUserAndCreatedAtRangeAsync(
        Id<User> userId,
        IReadOnlyCollection<BodyParts> bodyParts,
        DateTimeOffset createdAtFromUtc,
        DateTimeOffset createdAtToUtc,
        CancellationToken cancellationToken = default)
    {
        if (bodyParts.Count == 0)
        {
            return [];
        }

        var existingBodyParts = await _dbContext.Measurements
            .AsNoTracking()
            .Where(measurement =>
                measurement.UserId == userId
                && bodyParts.Contains(measurement.BodyPart)
                && measurement.CreatedAt >= createdAtFromUtc
                && measurement.CreatedAt < createdAtToUtc)
            .Select(measurement => measurement.BodyPart)
            .Distinct()
            .ToListAsync(cancellationToken);

        return existingBodyParts.ToHashSet();
    }
}
