using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Repositories;

public interface IPushInstallationRepository
{
    Task<PushInstallation?> FindByIdAsync(Id<PushInstallation> id, CancellationToken cancellationToken = default);
    Task<PushInstallation?> FindByInstallationIdAsync(string installationId, CancellationToken cancellationToken = default);
    Task<PushInstallation?> FindBoundToUserOrSessionAsync(string installationId, Id<User> userId, Id<UserSession> sessionId, CancellationToken cancellationToken = default);
    Task<List<PushInstallation>> GetActiveByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default);
    Task<List<PushInstallation>> GetBySessionIdAsync(Id<UserSession> sessionId, CancellationToken cancellationToken = default);
    Task<List<PushInstallation>> GetStaleActiveAsync(DateTimeOffset lastSeenBefore, int limit, CancellationToken cancellationToken = default);
    Task AddAsync(PushInstallation installation, CancellationToken cancellationToken = default);
    Task UpdateAsync(PushInstallation installation, CancellationToken cancellationToken = default);
}
