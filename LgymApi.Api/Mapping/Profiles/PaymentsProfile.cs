using LgymApi.Api.Features.Payments.Contracts;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Payments.Contracts;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class PaymentsProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<CreatePaymentCheckoutRequest, CreatePaymentCheckoutRequest>((source, _) => source);
        configuration.CreateMap<PaymentCheckoutStatusRequest, Id<PaymentCheckout>>((source, _) =>
            Id<PaymentCheckout>.TryParse(source.CheckoutId, out var checkoutId) ? checkoutId : Id<PaymentCheckout>.Empty);
        configuration.CreateMap<PaymentCheckoutStartResult, PaymentCheckoutResponse>((source, _) => new PaymentCheckoutResponse
        {
            CheckoutId = source.CheckoutId.ToString(),
            CheckoutUrl = source.CheckoutUrl
        });
        configuration.CreateMap<PaymentCheckoutStatusResult, PaymentCheckoutStatusResponse>((source, _) => new PaymentCheckoutStatusResponse
        {
            CheckoutId = source.CheckoutId.ToString(),
            Status = source.Status
        });
    }
}
