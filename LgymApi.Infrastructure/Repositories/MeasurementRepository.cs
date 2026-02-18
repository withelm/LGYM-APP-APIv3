using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
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

    public async Task AddAsync(Measurement measurement, CancellationToken cancellationToken = default)
    {
        await _dbContext.Measurements.AddAsync(measurement, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Measurement?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Measurements.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public Task<List<Measurement>> GetByUserAsync(Guid userId, BodyParts? bodyPart, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Measurements.Where(m => m.UserId == userId).AsQueryable();
        if (bodyPart.HasValue)
        {
            query = query.Where(m => m.BodyPart == bodyPart.Value);
        }

        return query.ToListAsync(cancellationToken);
    }
}
