using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class PushInstallationRepository : IPushInstallationRepository
{
    private readonly AppDbContext _dbContext;

    public PushInstallationRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<PushInstallation?> FindByIdAsync(Id<PushInstallation> id, CancellationToken cancellationToken = default)
    {
        return _dbContext.PushInstallations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<PushInstallation?> FindByInstallationIdAsync(string installationId, CancellationToken cancellationToken = default)
    {
        return _dbContext.PushInstallations
            .FirstOrDefaultAsync(x => x.InstallationId == installationId, cancellationToken);
    }

    public Task<PushInstallation?> FindBoundToUserOrSessionAsync(
        string installationId,
        Id<User> userId,
        Id<UserSession> sessionId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.PushInstallations
            .FirstOrDefaultAsync(
                x => x.InstallationId == installationId
                    && (x.UserId == userId || x.SessionId == sessionId),
                cancellationToken);
    }

    public Task<List<PushInstallation>> GetBySessionIdAsync(Id<UserSession> sessionId, CancellationToken cancellationToken = default)
    {
        return _dbContext.PushInstallations
            .Where(x => x.SessionId == sessionId)
            .ToListAsync(cancellationToken);
    }

    public Task<List<PushInstallation>> GetActiveByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.PushInstallations
            .Where(x => x.UserId == userId && x.DisabledAt == null)
            .ToListAsync(cancellationToken);
    }

    public Task<List<PushInstallation>> GetStaleActiveAsync(DateTimeOffset lastSeenBefore, int limit, CancellationToken cancellationToken = default)
    {
        return _dbContext.PushInstallations
            .Where(x => x.DisabledAt == null && x.LastSeenAt < lastSeenBefore)
            .OrderBy(x => x.LastSeenAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(PushInstallation installation, CancellationToken cancellationToken = default)
    {
        return _dbContext.PushInstallations.AddAsync(installation, cancellationToken).AsTask();
    }

    public Task UpdateAsync(PushInstallation installation, CancellationToken cancellationToken = default)
    {
        _dbContext.PushInstallations.Update(installation);
        return Task.CompletedTask;
    }
}
