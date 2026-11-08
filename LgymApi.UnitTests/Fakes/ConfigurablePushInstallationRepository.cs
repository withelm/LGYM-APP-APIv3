using LgymApi.Application.Notifications.Repositories;
using LgymApi.Application.Notifications.Contracts.Push;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;

namespace LgymApi.UnitTests.Fakes;

internal sealed class ConfigurablePushInstallationRepository : IPushInstallationRepository
{
    public List<(string Method, object? Argument, CancellationToken CancellationToken)> Calls { get; } = [];
    public Func<Id<PushInstallation>, CancellationToken, Task<PushInstallation?>> FindById { get; set; } = (_, _) => Task.FromResult<PushInstallation?>(null);
    public Func<Id<PushInstallation>, CancellationToken, Task<PushInstallation?>> FindActiveById { get; set; } = (_, _) => Task.FromResult<PushInstallation?>(null);
    public Func<Id<PushInstallation>, Id<User>, Id<UserSession>, long, CancellationToken, Task<PushInstallationDeliveryTarget?>> FindActiveBoundTarget { get; set; } = (_, _, _, _, _) => Task.FromResult<PushInstallationDeliveryTarget?>(null);
    public Func<string, CancellationToken, Task<PushInstallation?>> FindByInstallationId { get; set; } = (_, _) => Task.FromResult<PushInstallation?>(null);
    public Func<string, Id<User>, Id<UserSession>, CancellationToken, Task<PushInstallation?>> FindBoundToUserAndSession { get; set; } = (_, _, _, _) => Task.FromResult<PushInstallation?>(null);
    public Func<Id<User>, CancellationToken, Task<List<PushInstallation>>> GetActiveByUserId { get; set; } = (_, _) => Task.FromResult(new List<PushInstallation>());
    public Func<Id<UserSession>, CancellationToken, Task<List<PushInstallation>>> GetBySessionId { get; set; } = (_, _) => Task.FromResult(new List<PushInstallation>());
    public Func<DateTimeOffset, int, CancellationToken, Task<List<PushInstallation>>> GetStaleActive { get; set; } = (_, _, _) => Task.FromResult(new List<PushInstallation>());
    public Func<PushInstallation, CancellationToken, Task> Add { get; set; } = (_, _) => Task.CompletedTask;
    public Func<PushInstallation, CancellationToken, Task> Update { get; set; } = (_, _) => Task.CompletedTask;
    public Func<PushInstallationRegistration, CancellationToken, Task<PushInstallation?>> UpsertForUserSession { get; set; } = (_, _) => Task.FromResult<PushInstallation?>(new PushInstallation());
    public Func<string, Id<User>, Id<UserSession>, DateTimeOffset, string, CancellationToken, Task<PushInstallation?>> DisableBoundForUserAndSession { get; set; } = (_, _, _, _, _, _) => Task.FromResult<PushInstallation?>(new PushInstallation());
    public Func<string, Id<User>, Id<UserSession>, DateTimeOffset, CancellationToken, Task<PushInstallation?>> DisassociateBoundForUserAndSession { get; set; } = (_, _, _, _, _) => Task.FromResult<PushInstallation?>(new PushInstallation());
    public Func<PushInstallation, CancellationToken, Task> ResetFailedMutation { get; set; } = (_, _) => Task.CompletedTask;
    public Func<Id<AccountSessionReference>, DateTimeOffset, CancellationToken, Task> DisassociateForSession { get; set; } = (_, _, _) => Task.CompletedTask;
    public Func<Id<User>, CancellationToken, Task> RemoveForAccount { get; set; } = (_, _) => Task.CompletedTask;

    public Task<PushInstallation?> FindByIdAsync(Id<PushInstallation> id, CancellationToken cancellationToken = default) { Calls.Add((nameof(FindByIdAsync), id, cancellationToken)); return FindById(id, cancellationToken); }
    public Task<PushInstallation?> FindActiveByIdAsync(Id<PushInstallation> id, CancellationToken cancellationToken = default) { Calls.Add((nameof(FindActiveByIdAsync), id, cancellationToken)); return FindActiveById(id, cancellationToken); }
    public Task<PushInstallationDeliveryTarget?> FindActiveBoundTargetAsync(Id<PushInstallation> id, Id<User> userId, Id<UserSession> sessionId, long registrationRevision, CancellationToken cancellationToken = default) { Calls.Add((nameof(FindActiveBoundTargetAsync), id, cancellationToken)); return FindActiveBoundTarget(id, userId, sessionId, registrationRevision, cancellationToken); }
    public Task<PushInstallation?> FindByInstallationIdAsync(string installationId, CancellationToken cancellationToken = default) { Calls.Add((nameof(FindByInstallationIdAsync), installationId, cancellationToken)); return FindByInstallationId(installationId, cancellationToken); }
    public Task<PushInstallation?> FindBoundToUserAndSessionAsync(string installationId, Id<User> userId, Id<UserSession> sessionId, CancellationToken cancellationToken = default) { Calls.Add((nameof(FindBoundToUserAndSessionAsync), (installationId, userId, sessionId), cancellationToken)); return FindBoundToUserAndSession(installationId, userId, sessionId, cancellationToken); }
    public Task<List<PushInstallation>> GetActiveByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default) { Calls.Add((nameof(GetActiveByUserIdAsync), userId, cancellationToken)); return GetActiveByUserId(userId, cancellationToken); }
    public Task<List<PushInstallation>> GetBySessionIdAsync(Id<UserSession> sessionId, CancellationToken cancellationToken = default) { Calls.Add((nameof(GetBySessionIdAsync), sessionId, cancellationToken)); return GetBySessionId(sessionId, cancellationToken); }
    public Task<List<PushInstallation>> GetStaleActiveAsync(DateTimeOffset lastSeenBefore, int limit, CancellationToken cancellationToken = default) { Calls.Add((nameof(GetStaleActiveAsync), (lastSeenBefore, limit), cancellationToken)); return GetStaleActive(lastSeenBefore, limit, cancellationToken); }
    public Task AddAsync(PushInstallation installation, CancellationToken cancellationToken = default) { Calls.Add((nameof(AddAsync), installation, cancellationToken)); return Add(installation, cancellationToken); }
    public Task UpdateAsync(PushInstallation installation, CancellationToken cancellationToken = default) { Calls.Add((nameof(UpdateAsync), installation, cancellationToken)); return Update(installation, cancellationToken); }
    public Task<PushInstallation?> UpsertForUserSessionAsync(PushInstallationRegistration registration, CancellationToken cancellationToken = default) { Calls.Add((nameof(UpsertForUserSessionAsync), registration, cancellationToken)); return UpsertForUserSession(registration, cancellationToken); }
    public Task<PushInstallation?> DisableBoundForUserAndSessionAsync(string installationId, Id<User> userId, Id<UserSession> sessionId, DateTimeOffset disabledAt, string disabledReason, CancellationToken cancellationToken = default) { Calls.Add((nameof(DisableBoundForUserAndSessionAsync), (installationId, userId, sessionId, disabledAt, disabledReason), cancellationToken)); return DisableBoundForUserAndSession(installationId, userId, sessionId, disabledAt, disabledReason, cancellationToken); }
    public Task<PushInstallation?> DisassociateBoundForUserAndSessionAsync(string installationId, Id<User> userId, Id<UserSession> sessionId, DateTimeOffset lastSeenAt, CancellationToken cancellationToken = default) { Calls.Add((nameof(DisassociateBoundForUserAndSessionAsync), (installationId, userId, sessionId, lastSeenAt), cancellationToken)); return DisassociateBoundForUserAndSession(installationId, userId, sessionId, lastSeenAt, cancellationToken); }
    public Task ResetFailedMutationAsync(PushInstallation installation, CancellationToken cancellationToken = default) { Calls.Add((nameof(ResetFailedMutationAsync), installation, cancellationToken)); return ResetFailedMutation(installation, cancellationToken); }
    public Task DisassociateForSessionAsync(Id<AccountSessionReference> sessionId, DateTimeOffset lastSeenAt, CancellationToken cancellationToken = default) { Calls.Add((nameof(DisassociateForSessionAsync), (sessionId, lastSeenAt), cancellationToken)); return DisassociateForSession(sessionId, lastSeenAt, cancellationToken); }
    public Task RemoveForAccountAsync(Id<User> userId, CancellationToken cancellationToken = default) { Calls.Add((nameof(RemoveForAccountAsync), userId, cancellationToken)); return RemoveForAccount(userId, cancellationToken); }
}
