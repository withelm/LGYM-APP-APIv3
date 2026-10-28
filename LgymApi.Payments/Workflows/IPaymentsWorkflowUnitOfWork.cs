namespace LgymApi.Payments.Workflows;

internal interface IPaymentsWorkflowUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
