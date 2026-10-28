using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;

namespace LgymApi.Payments.Contracts;

public sealed record PaymentAccountReference(Id<AccountReference> AccountId);
