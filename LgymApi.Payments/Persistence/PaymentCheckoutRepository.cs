using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Payments.Persistence;

internal sealed class PaymentCheckoutRepository(IPaymentsPersistenceContext persistenceContext) : IPaymentCheckoutRepository
{
    public Task AddAsync(PaymentCheckout checkout, CancellationToken cancellationToken = default) =>
        persistenceContext.PaymentCheckouts.AddAsync(checkout, cancellationToken).AsTask();

    public Task<PaymentCheckout?> FindByAccountAndClientIdempotencyKeyAsync(Id<User> accountId, string clientIdempotencyKey, CancellationToken cancellationToken = default) =>
        persistenceContext.PaymentCheckouts.SingleOrDefaultAsync(
            checkout => checkout.AccountId == accountId && checkout.ClientIdempotencyKey == clientIdempotencyKey,
            cancellationToken);

    public Task<PaymentCheckout?> FindByProviderSessionReferenceAsync(string providerSessionReference, CancellationToken cancellationToken = default) =>
        persistenceContext.PaymentCheckouts.AsNoTracking().SingleOrDefaultAsync(
            checkout => checkout.ProviderSessionReference == providerSessionReference,
            cancellationToken);

    public Task<PaymentCheckout?> FindForUpdateByProviderSessionReferenceAsync(string providerSessionReference, CancellationToken cancellationToken = default) =>
        persistenceContext.PaymentCheckouts.SingleOrDefaultAsync(
            checkout => checkout.ProviderSessionReference == providerSessionReference,
            cancellationToken);

    public Task<PaymentCheckout?> FindByIdAsync(Id<PaymentCheckout> checkoutId, CancellationToken cancellationToken = default) =>
        persistenceContext.PaymentCheckouts.AsNoTracking().SingleOrDefaultAsync(checkout => checkout.Id == checkoutId, cancellationToken);
}
