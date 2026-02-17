using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class AppConfigRepository : IAppConfigRepository
{
    private readonly AppDbContext _dbContext;

    public AppConfigRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<AppConfig?> GetLatestByPlatformAsync(Platforms platform, CancellationToken cancellationToken = default)
    {
        return _dbContext.AppConfigs
            .Where(c => c.Platform == platform)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task AddAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        return _dbContext.AppConfigs.AddAsync(config, cancellationToken).AsTask();
    }
}
