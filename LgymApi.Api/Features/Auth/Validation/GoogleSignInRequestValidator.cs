using FluentValidation;
using LgymApi.Api.Features.Auth.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Auth.Validation;

public sealed class GoogleSignInRequestValidator : AbstractValidator<GoogleSignInRequest>
{
    public GoogleSignInRequestValidator()
    {
        RuleFor(x => x.IdToken)
            .NotEmpty()
            .WithMessage(Messages.GoogleIdTokenRequired);
    }
}
