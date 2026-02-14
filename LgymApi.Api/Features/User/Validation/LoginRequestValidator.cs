using FluentValidation;
using LgymApi.Api.Features.User.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.User.Validation;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage(Messages.NameIsRequired);

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage(Messages.PasswordRequired);
    }
}
