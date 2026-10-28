using System.Text.Json;
using System.Text.Json.Serialization;
using LgymApi.Api.Interfaces;
using LgymApi.Domain.Enums;

namespace LgymApi.Api.Features.Payments.Contracts;

public sealed class CreatePaymentCheckoutRequest : IDto
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed class PaymentCheckoutStatusRequest : IDto
{
    public string CheckoutId { get; set; } = string.Empty;
}

public sealed class PaymentCheckoutResponse : IResultDto
{
    public string CheckoutId { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = string.Empty;
}

public sealed class PaymentCheckoutStatusResponse : IResultDto
{
    public string CheckoutId { get; set; } = string.Empty;
    public PaymentCheckoutStatus Status { get; set; }
}
