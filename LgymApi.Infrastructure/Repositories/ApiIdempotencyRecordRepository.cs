using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class ApiIdempotencyRecordRepository : IApiIdempotencyRecordRepository
{
    private readonly AppDbContext _dbContext;

    public ApiIdempotencyRecordRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ApiIdempotencyRecord?> FindByScopeAndKeyAsync(
        string scopeTuple,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ApiIdempotencyRecords
            .FirstOrDefaultAsync(
                x => x.ScopeTuple == scopeTuple && x.IdempotencyKey == idempotencyKey,
                cancellationToken);
    }

    public async Task<ApiIdempotencyRecord> AddOrGetExistingAsync(
        ApiIdempotencyRecord record,
        CancellationToken cancellationToken = default)
    {
        var existing = await FindByScopeAndKeyAsync(
            record.ScopeTuple,
            record.IdempotencyKey,
            cancellationToken);

        if (existing != null)
        {
            return existing;
        }

        await _dbContext.ApiIdempotencyRecords.AddAsync(record, cancellationToken);
        return record;
    }

    public Task UpdateAsync(
        ApiIdempotencyRecord record,
        CancellationToken cancellationToken = default)
    {
        _dbContext.ApiIdempotencyRecords.Update(record);
        return Task.CompletedTask;
    }
}
