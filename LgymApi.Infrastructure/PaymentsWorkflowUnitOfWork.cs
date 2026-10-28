using LgymApi.Application.Repositories;
using LgymApi.Payments.Workflows;

namespace LgymApi.Infrastructure;

internal sealed class PaymentsWorkflowUnitOfWork(IUnitOfWork unitOfWork) : IPaymentsWorkflowUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        unitOfWork.SaveChangesAsync(cancellationToken);
}
