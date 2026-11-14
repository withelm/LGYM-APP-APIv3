using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts.AccountDeletion;
using LgymApi.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Identity.AccountDeletion;

internal sealed class AccountDeletionScheduleConfirmation(
    IIdentityPersistenceContext context,
    IAccountDeletionSynchronization synchronization,
    IUnitOfWork unitOfWork) : IAccountDeletionScheduleConfirmation
{
    public async Task ExecuteAsync(AccountDeletionScheduledCommand command, CancellationToken cancellationToken = default)
    {
        if (command.AccountId.IsEmpty || command.DeletionGeneration.IsEmpty)
            throw new ArgumentException("Account deletion scheduling requires an account and generation.", nameof(command));

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await synchronization.AcquireAsync(command.AccountId, cancellationToken);
        var user = await context.Users.IgnoreQueryFilters()
            .SingleOrDefaultAsync(user => user.Id == command.AccountId.Rebind<User>(), cancellationToken);
        if (user is null || user.IsDeleted || user.DeletionState != AccountDeletionState.PendingDeletion
            || user.DeletionGeneration != command.DeletionGeneration
            || user.DeletionScheduledGeneration == command.DeletionGeneration)
            return;

        user.DeletionScheduledGeneration = command.DeletionGeneration;
        user.LifecycleRevision = Id<CorrelationScope>.New();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
