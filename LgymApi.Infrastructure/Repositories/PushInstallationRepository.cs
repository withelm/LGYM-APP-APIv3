using LgymApi.Application.Repositories;
using LgymApi.Application.Notifications.Repositories;
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

    public async Task UpsertForUserSessionAsync(PushInstallationRegistration registration, CancellationToken cancellationToken = default)
    {
        var installation = await _dbContext.PushInstallations
            .FirstOrDefaultAsync(entity => entity.InstallationId == registration.InstallationId, cancellationToken);

        var isNewInstallation = installation == null;
        if (installation == null)
        {
            installation = new PushInstallation
            {
                Id = Id<PushInstallation>.New()
            };

            await _dbContext.PushInstallations.AddAsync(installation, cancellationToken);
        }

        installation.UserId = registration.UserId;
        installation.SessionId = registration.SessionId;
        installation.InstallationId = registration.InstallationId;
        installation.Platform = registration.Platform;
        installation.FcmToken = registration.FcmToken;
        installation.AppVersion = registration.AppVersion;
        installation.Environment = registration.Environment;
        installation.PermissionStatus = registration.PermissionStatus;
        installation.LastSeenAt = registration.LastSeenAt;
        installation.DisabledAt = null;
        installation.DisabledReason = null;

        if (!isNewInstallation)
        {
            _dbContext.PushInstallations.Update(installation);
        }
    }

    public async Task<bool> DisableBoundForUserOrSessionAsync(
        string installationId,
        Id<User> userId,
        Id<UserSession> sessionId,
        DateTimeOffset disabledAt,
        string disabledReason,
        CancellationToken cancellationToken = default)
    {
        var installation = await FindBoundToUserOrSessionAsync(installationId, userId, sessionId, cancellationToken);
        if (installation == null)
        {
            return false;
        }

        installation.DisabledAt = disabledAt;
        installation.DisabledReason = disabledReason;
        installation.LastSeenAt = disabledAt;
        _dbContext.PushInstallations.Update(installation);
        return true;
    }

    public async Task<bool> DisassociateBoundForUserOrSessionAsync(
        string installationId,
        Id<User> userId,
        Id<UserSession> sessionId,
        DateTimeOffset lastSeenAt,
        CancellationToken cancellationToken = default)
    {
        var installation = await FindBoundToUserOrSessionAsync(installationId, userId, sessionId, cancellationToken);
        if (installation == null)
        {
            return false;
        }

        Disassociate(installation, lastSeenAt);
        _dbContext.PushInstallations.Update(installation);
        return true;
    }

    public async Task DisassociateForSessionAsync(
        Id<UserSession> sessionId,
        DateTimeOffset lastSeenAt,
        CancellationToken cancellationToken = default)
    {
        var installations = await GetBySessionIdAsync(sessionId, cancellationToken);
        foreach (var installation in installations)
        {
            Disassociate(installation, lastSeenAt);
        }
    }

    private static void Disassociate(PushInstallation installation, DateTimeOffset lastSeenAt)
    {
        installation.UserId = null;
        installation.SessionId = null;
        installation.LastSeenAt = lastSeenAt;
    }
}
