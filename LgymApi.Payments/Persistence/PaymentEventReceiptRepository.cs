using LgymApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Payments.Persistence;

internal sealed class PaymentEventReceiptRepository(IPaymentsPersistenceContext persistenceContext) : IPaymentEventReceiptRepository
{
    public Task AddAsync(PaymentEventReceipt receipt, CancellationToken cancellationToken = default) =>
        persistenceContext.PaymentEventReceipts.AddAsync(receipt, cancellationToken).AsTask();

    public Task<PaymentEventReceipt?> FindByProviderEventIdAsync(string providerEventId, CancellationToken cancellationToken = default) =>
        persistenceContext.PaymentEventReceipts.AsNoTracking().SingleOrDefaultAsync(
            receipt => receipt.ProviderEventId == providerEventId,
            cancellationToken);
}
