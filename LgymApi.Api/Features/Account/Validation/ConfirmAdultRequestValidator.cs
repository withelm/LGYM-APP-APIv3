using FluentValidation;
using LgymApi.Api.Features.Account.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Account.Validation;

public sealed class ConfirmAdultRequestValidator : AbstractValidator<ConfirmAdultRequest>
{
    public ConfirmAdultRequestValidator()
    {
        RuleFor(x => x.AdultConfirmed)
            .Equal(true)
            .WithMessage(Messages.AdultConfirmationRequired);
    }
}
