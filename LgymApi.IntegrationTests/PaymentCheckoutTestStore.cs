using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Payments.Contracts;

namespace LgymApi.IntegrationTests;

internal static class PaymentCheckoutTestStore
{
    private static readonly Dictionary<Id<PaymentCheckout>, Id<User>> Owners = [];
    internal static int ProcessedWebhookCount { get; private set; }
    internal static void Reset() { Owners.Clear(); ProcessedWebhookCount = 0; }
    internal static PaymentCheckout Create(Id<User> accountId)
    {
        var checkout = new PaymentCheckout { Id = Id<PaymentCheckout>.New(), AccountId = accountId, Status = PaymentCheckoutStatus.Created };
        Owners[checkout.Id] = accountId;
        return checkout;
    }
    internal static bool IsOwner(Id<User> accountId, Id<PaymentCheckout> checkoutId) => Owners.TryGetValue(checkoutId, out var owner) && owner == accountId;
    internal static void ProcessWebhook() => ProcessedWebhookCount++;
}

internal sealed class TestPaymentsApi : IPaymentsApi
{
    public Task<PaymentCheckoutStartResult> CreateCheckoutAsync(PaymentAccountReference account, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var accountId = Id<User>.TryParse(account.AccountId.ToString(), out var parsedAccountId) ? parsedAccountId : Id<User>.Empty;
        var checkout = PaymentCheckoutTestStore.Create(accountId);
        return Task.FromResult(new PaymentCheckoutStartResult(checkout.Id, "https://checkout.test.local/session"));
    }
    public Task<PaymentCheckoutStatusResult?> GetCheckoutStatusAsync(PaymentAccountReference account, Id<PaymentCheckout> checkoutId, CancellationToken cancellationToken = default)
    {
        var accountId = Id<User>.TryParse(account.AccountId.ToString(), out var parsedAccountId) ? parsedAccountId : Id<User>.Empty;
        return Task.FromResult<PaymentCheckoutStatusResult?>(PaymentCheckoutTestStore.IsOwner(accountId, checkoutId) ? new PaymentCheckoutStatusResult(checkoutId, PaymentCheckoutStatus.Created) : null);
    }
    public Task<PaymentWebhookResult> HandleWebhookAsync(ReadOnlyMemory<byte> rawBody, string signature, CancellationToken cancellationToken = default)
    {
        if (signature != "test-valid-signature" || !rawBody.Span.SequenceEqual("valid-payment-event"u8)) return Task.FromResult(PaymentWebhookResult.BadRequest);
        if (PaymentCheckoutTestStore.ProcessedWebhookCount == 0) PaymentCheckoutTestStore.ProcessWebhook();
        return Task.FromResult(PaymentWebhookResult.Processed);
    }
}
