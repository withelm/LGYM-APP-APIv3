using System.Security.Cryptography;
using System.Text;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.AccountDeletion;
using LgymApi.Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using LgymApi.Resources;

namespace LgymApi.Identity.AccountDeletion;

internal sealed class AccountDeletionService(
    IIdentityPersistenceContext context,
    IUnitOfWork unitOfWork,
    ILegacyPasswordService passwords,
    TimeProvider clock,
    IAccountDeletionSynchronization synchronization,
    IAccountRecoveryEmailOutbox recoveryEmails) : IAccountDeletionService
{
    public async Task RequestAsync(Id<AccountReference> accountId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await synchronization.AcquireAsync(accountId, cancellationToken);
        var user = await context.Users.FirstOrDefaultAsync(x => x.Id == accountId.Rebind<User>(), cancellationToken)
            ?? throw new UnauthorizedAccessException(Messages.Unauthorized);
        if (user.DeletionState == AccountDeletionState.PendingDeletion)
        {
            return;
        }
        if (user.IsDeleted || user.DeletionState != AccountDeletionState.Active)
        {
            throw new UnauthorizedAccessException(Messages.Unauthorized);
        }

        var now = clock.GetUtcNow();
        user.DeletionState = AccountDeletionState.PendingDeletion;
        user.DeletionRequestedAt = now;
        user.DeletionEffectiveAt = now.AddDays(30);
        user.DeletionGeneration = Id<CorrelationScope>.New();
        user.LifecycleRevision = Id<CorrelationScope>.New();
        var sessions = await context.UserSessions.Where(x => x.UserId == user.Id && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.RevokedAtUtc = now;
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<string> SignInWithPasswordAsync(string name, string password, CancellationToken cancellationToken = default)
    {
        var user = await context.Users.FirstOrDefaultAsync(x => x.Name == name, cancellationToken);
        var now = clock.GetUtcNow();
        if (user is null || user.IsDeleted || user.IsBlocked || user.DeletionState != AccountDeletionState.PendingDeletion
            || user.DeletionEffectiveAt is null || now >= user.DeletionEffectiveAt || user.DeletionGeneration is null
            || string.IsNullOrEmpty(user.LegacyHash) || string.IsNullOrEmpty(user.LegacySalt)
            || !passwords.Verify(password, user.LegacyHash, user.LegacySalt, user.LegacyIterations, user.LegacyKeyLength, user.LegacyDigest))
        {
            throw new UnauthorizedAccessException(Messages.InvalidToken);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await synchronization.AcquireAsync(user.Id.Rebind<AccountReference>(), cancellationToken);
        now = clock.GetUtcNow();
        if (user.DeletionState != AccountDeletionState.PendingDeletion || user.DeletionEffectiveAt <= now || user.DeletionGeneration is null)
            throw new UnauthorizedAccessException(Messages.InvalidToken);
        var (token, _) = StageRecoverySession(user, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return token;
    }

    public async Task RequestRecoveryEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var address = new Email(email);
        var user = await context.Users.FirstOrDefaultAsync(candidate => candidate.Email == address, cancellationToken);
        if (user is null) return;
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await synchronization.AcquireAsync(user.Id.Rebind<AccountReference>(), cancellationToken);
        var now = clock.GetUtcNow();
        if (user.IsDeleted || user.IsBlocked || user.DeletionState != AccountDeletionState.PendingDeletion
            || user.DeletionGeneration is null || user.DeletionEffectiveAt is null || user.DeletionEffectiveAt <= now) return;
        var recent = now.AddMinutes(-1);
        if (await context.UserSessions.AnyAsync(session => session.UserId == user.Id && session.RecoveryTokenHash != null
            && session.CreatedAt > recent && session.RevokedAtUtc == null && session.ExpiresAtUtc > now, cancellationToken)) return;
        var (token, correlationId) = StageRecoverySession(user, now);
        await recoveryEmails.StageAsync(new AccountRecoveryEmail(user.Id.Rebind<AccountReference>(), correlationId,
            user.Email.Value, token, user.PreferredLanguage.StartsWith("pl", StringComparison.OrdinalIgnoreCase) ? "pl" : "en"), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private (string Token, Id<CorrelationScope> CorrelationId) StageRecoverySession(User user, DateTimeOffset now)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expiry = now.AddMinutes(30);
        var sessionId = Id<UserSession>.New();
        context.UserSessions.Add(new UserSession
        {
            Id = sessionId, UserId = user.Id, Jti = Id<UserSession>.New().ToString(), CreatedAt = now,
            ExpiresAtUtc = expiry < user.DeletionEffectiveAt ? expiry : user.DeletionEffectiveAt!.Value,
            RecoveryTokenHash = Hash(token), RecoveryDeletionGeneration = user.DeletionGeneration
        });
        user.LifecycleRevision = Id<CorrelationScope>.New();
        return (token, sessionId.Rebind<CorrelationScope>());
    }

    public async Task CancelAsync(string recoveryToken, CancellationToken cancellationToken = default)
    {
        var (_, initialUser) = await FindRecoveryAsync(recoveryToken, cancellationToken);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await synchronization.AcquireAsync(initialUser.Id.Rebind<AccountReference>(), cancellationToken);
        var (_, user) = await FindRecoveryAsync(recoveryToken, cancellationToken);
        var now = clock.GetUtcNow();
        if (user.DeletionEffectiveAt is null || now >= user.DeletionEffectiveAt)
        {
            throw new UnauthorizedAccessException(Messages.InvalidToken);
        }
        user.DeletionState = AccountDeletionState.Active;
        user.DeletionRequestedAt = null;
        user.DeletionEffectiveAt = null;
        user.DeletionGeneration = null;
        user.LifecycleRevision = Id<CorrelationScope>.New();
        var sessions = await context.UserSessions.Where(session => session.UserId == user.Id && session.RecoveryTokenHash != null
            && session.RevokedAtUtc == null).ToListAsync(cancellationToken);
        foreach (var session in sessions) session.RevokedAtUtc = now;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task LogoutRecoveryAsync(string recoveryToken, CancellationToken cancellationToken = default)
    {
        var (_, initialUser) = await FindRecoveryAsync(recoveryToken, cancellationToken);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await synchronization.AcquireAsync(initialUser.Id.Rebind<AccountReference>(), cancellationToken);
        var (session, user) = await FindRecoveryAsync(recoveryToken, cancellationToken);
        session.RevokedAtUtc = clock.GetUtcNow();
        user.LifecycleRevision = Id<CorrelationScope>.New();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<(UserSession Session, User User)> FindRecoveryAsync(string token, CancellationToken cancellationToken)
    {
        var hash = Hash(token);
        var now = clock.GetUtcNow();
        var session = await context.UserSessions.FirstOrDefaultAsync(x => x.RecoveryTokenHash == hash
            && x.RevokedAtUtc == null && !x.IsDeleted && x.ExpiresAtUtc > now, cancellationToken)
            ?? throw new UnauthorizedAccessException(Messages.InvalidToken);
        var user = await context.Users.FirstOrDefaultAsync(x => x.Id == session.UserId, cancellationToken);
        if (user is null || user.IsDeleted || user.IsBlocked || user.DeletionState != AccountDeletionState.PendingDeletion
            || user.DeletionGeneration is null || user.DeletionGeneration != session.RecoveryDeletionGeneration)
        {
            throw new UnauthorizedAccessException(Messages.InvalidToken);
        }
        return (session, user);
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
