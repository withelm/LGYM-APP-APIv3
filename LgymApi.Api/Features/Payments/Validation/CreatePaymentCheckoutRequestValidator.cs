using FluentValidation;
using LgymApi.Api.Features.Payments.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Payments.Validation;

public sealed class CreatePaymentCheckoutRequestValidator : AbstractValidator<CreatePaymentCheckoutRequest>
{
    public CreatePaymentCheckoutRequestValidator()
    {
        RuleFor(request => request.AdditionalProperties)
            .Must(properties => properties is null || properties.Count == 0)
            .WithMessage(Messages.PaymentCheckoutClientFieldsNotAllowed);
    }
}
