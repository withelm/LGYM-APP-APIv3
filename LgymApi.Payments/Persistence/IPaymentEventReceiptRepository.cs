using LgymApi.Domain.Entities;

namespace LgymApi.Payments.Persistence;

internal interface IPaymentEventReceiptRepository
{
    Task AddAsync(PaymentEventReceipt receipt, CancellationToken cancellationToken = default);
    Task<PaymentEventReceipt?> FindByProviderEventIdAsync(string providerEventId, CancellationToken cancellationToken = default);
}
