using LgymApi.Application.Notifications.Repositories;
using LgymApi.Application.Notifications.Contracts.Push;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Notifications.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

internal sealed class PushInstallationRepository : IPushInstallationRepository
{
    private readonly INotificationsPersistenceContext _persistenceContext;

    public PushInstallationRepository(INotificationsPersistenceContext persistenceContext)
    {
        _persistenceContext = persistenceContext;
    }

    public Task<PushInstallation?> FindByIdAsync(Id<PushInstallation> id, CancellationToken cancellationToken = default)
    {
        return _persistenceContext.PushInstallations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<PushInstallation?> FindActiveByIdAsync(Id<PushInstallation> id, CancellationToken cancellationToken = default)
    {
        return _persistenceContext.PushInstallations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.DisabledAt == null, cancellationToken);
    }

    public Task<PushInstallationDeliveryTarget?> FindActiveBoundTargetAsync(
        Id<PushInstallation> id,
        Id<User> userId,
        Id<UserSession> sessionId,
        long registrationRevision,
        CancellationToken cancellationToken = default)
    {
        return _persistenceContext.PushInstallations
            .AsNoTracking()
            .Where(x => x.Id == id
                && x.UserId == userId
                && x.SessionId == sessionId
                && x.RegistrationRevision == registrationRevision
                && x.DisabledAt == null)
            .Select(x => new PushInstallationDeliveryTarget(
                x.Id,
                x.UserId!.Value,
                x.SessionId!.Value,
                x.RegistrationRevision,
                x.FcmToken))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<PushInstallation?> FindByInstallationIdAsync(string installationId, CancellationToken cancellationToken = default)
    {
        return _persistenceContext.PushInstallations
            .FirstOrDefaultAsync(x => x.InstallationId == installationId, cancellationToken);
    }

    public Task<PushInstallation?> FindBoundToUserAndSessionAsync(
        string installationId,
        Id<User> userId,
        Id<UserSession> sessionId,
        CancellationToken cancellationToken = default)
    {
        return _persistenceContext.PushInstallations
            .FirstOrDefaultAsync(
                x => x.InstallationId == installationId
                    && x.UserId == userId
                    && x.SessionId == sessionId,
                cancellationToken);
    }

    public Task<List<PushInstallation>> GetBySessionIdAsync(Id<UserSession> sessionId, CancellationToken cancellationToken = default)
    {
        return _persistenceContext.PushInstallations
            .Where(x => x.SessionId == sessionId)
            .ToListAsync(cancellationToken);
    }

    public Task<List<PushInstallation>> GetActiveByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default)
    {
        return _persistenceContext.PushInstallations
            .Where(x => x.UserId == userId && x.DisabledAt == null)
            .ToListAsync(cancellationToken);
    }

    public Task<List<PushInstallation>> GetStaleActiveAsync(DateTimeOffset lastSeenBefore, int limit, CancellationToken cancellationToken = default)
    {
        return _persistenceContext.PushInstallations
            .Where(x => x.DisabledAt == null && x.LastSeenAt < lastSeenBefore)
            .OrderBy(x => x.LastSeenAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(PushInstallation installation, CancellationToken cancellationToken = default)
    {
        return _persistenceContext.PushInstallations.AddAsync(installation, cancellationToken).AsTask();
    }

    public Task UpdateAsync(PushInstallation installation, CancellationToken cancellationToken = default)
    {
        _persistenceContext.PushInstallations.Update(installation);
        return Task.CompletedTask;
    }

    public async Task<PushInstallation?> UpsertForUserSessionAsync(PushInstallationRegistration registration, CancellationToken cancellationToken = default)
    {
        var installation = await _persistenceContext.PushInstallations
            .FirstOrDefaultAsync(entity => entity.InstallationId == registration.InstallationId, cancellationToken);

        var tokenInstallation = await _persistenceContext.PushInstallations
            .FirstOrDefaultAsync(entity => entity.FcmToken == registration.FcmToken, cancellationToken);
        if (tokenInstallation != null && tokenInstallation.Id != installation?.Id)
        {
            return null;
        }

        if (installation != null
            && ((installation.UserId.HasValue && installation.UserId != registration.UserId)
                || (!installation.UserId.HasValue && installation.SessionId.HasValue)))
        {
            return null;
        }

        var isNewInstallation = installation == null;
        if (installation == null)
        {
            installation = new PushInstallation
            {
                Id = Id<PushInstallation>.New()
            };

            await _persistenceContext.PushInstallations.AddAsync(installation, cancellationToken);
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
            _persistenceContext.PushInstallations.Update(installation);
        }

        return installation;
    }

    public async Task<PushInstallation?> DisableBoundForUserAndSessionAsync(
        string installationId,
        Id<User> userId,
        Id<UserSession> sessionId,
        DateTimeOffset disabledAt,
        string disabledReason,
        CancellationToken cancellationToken = default)
    {
        var installation = await FindBoundToUserAndSessionAsync(installationId, userId, sessionId, cancellationToken);
        if (installation == null)
        {
            return null;
        }

        installation.DisabledAt = disabledAt;
        installation.DisabledReason = disabledReason;
        installation.LastSeenAt = disabledAt;
        _persistenceContext.PushInstallations.Update(installation);
        return installation;
    }

    public async Task<PushInstallation?> DisassociateBoundForUserAndSessionAsync(
        string installationId,
        Id<User> userId,
        Id<UserSession> sessionId,
        DateTimeOffset lastSeenAt,
        CancellationToken cancellationToken = default)
    {
        var installation = await FindBoundToUserAndSessionAsync(installationId, userId, sessionId, cancellationToken);
        if (installation == null)
        {
            return null;
        }

        Disassociate(installation, lastSeenAt);
        _persistenceContext.PushInstallations.Update(installation);
        return installation;
    }

    public async Task ResetFailedMutationAsync(
        PushInstallation installation,
        CancellationToken cancellationToken = default)
    {
        var entry = _persistenceContext.Entry(installation);
        if (entry.State == EntityState.Added)
        {
            entry.State = EntityState.Detached;
            return;
        }

        if (entry.State is EntityState.Modified or EntityState.Deleted)
        {
            await entry.ReloadAsync(cancellationToken);
        }
    }

    public async Task DisassociateForSessionAsync(
        Id<AccountSessionReference> sessionId,
        DateTimeOffset lastSeenAt,
        CancellationToken cancellationToken = default)
    {
        var installations = await GetBySessionIdAsync(sessionId.Rebind<UserSession>(), cancellationToken);
        foreach (var installation in installations)
        {
            Disassociate(installation, lastSeenAt);
        }
    }

    public async Task RemoveForAccountAsync(Id<User> userId, CancellationToken cancellationToken = default)
    {
        var installations = await _persistenceContext.PushInstallations
            .IgnoreQueryFilters()
            .Where(installation => installation.UserId == userId)
            .ToListAsync(cancellationToken);
        var installationIds = installations.Select(installation => installation.Id).ToHashSet();
        var messages = await _persistenceContext.PushNotificationMessages
            .IgnoreQueryFilters()
            .Where(message => installationIds.Contains(message.PushInstallationId))
            .ToListAsync(cancellationToken);

        _persistenceContext.PushNotificationMessages.RemoveRange(messages);
        _persistenceContext.PushInstallations.RemoveRange(installations);
    }

    public async Task<IReadOnlyList<PushInstallation>> GetRetentionCandidatesDisabledBeforeAsync(
        DateTimeOffset cutoff,
        int candidateLimit,
        CancellationToken cancellationToken = default)
    {
        return await _persistenceContext.PushInstallations
            .IgnoreQueryFilters()
            .Where(installation => installation.DisabledAt != null && installation.DisabledAt < cutoff)
            .OrderBy(installation => installation.DisabledAt)
            .ThenBy(installation => installation.Id)
            .Take(candidateLimit)
            .ToListAsync(cancellationToken);
    }

    public void RemoveRange(IEnumerable<PushInstallation> installations)
    {
        _persistenceContext.PushInstallations.RemoveRange(installations);
    }

    private static void Disassociate(PushInstallation installation, DateTimeOffset lastSeenAt)
    {
        installation.UserId = null;
        installation.SessionId = null;
        installation.LastSeenAt = lastSeenAt;
    }
}
