using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Payments.Persistence;

internal interface IPaymentCheckoutRepository
{
    Task AddAsync(PaymentCheckout checkout, CancellationToken cancellationToken = default);
    Task<PaymentCheckout?> FindByAccountAndClientIdempotencyKeyAsync(Id<User> accountId, string clientIdempotencyKey, CancellationToken cancellationToken = default);
    Task<PaymentCheckout?> FindByProviderSessionReferenceAsync(string providerSessionReference, CancellationToken cancellationToken = default);
    Task<PaymentCheckout?> FindForUpdateByProviderSessionReferenceAsync(string providerSessionReference, CancellationToken cancellationToken = default);
    Task<PaymentCheckout?> FindByIdAsync(Id<PaymentCheckout> checkoutId, CancellationToken cancellationToken = default);
}
