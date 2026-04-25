using FluentValidation;
using LgymApi.Api.Features.Account.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Account.Validation;

public sealed class LinkGoogleRequestValidator : AbstractValidator<LinkGoogleRequest>
{
    public LinkGoogleRequestValidator()
    {
        RuleFor(x => x.IdToken)
            .NotEmpty()
            .WithMessage(Messages.GoogleIdTokenRequired);
    }
}
