using FluentValidation;
using LgymApi.Api.Features.User.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.User.Validation;

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty()
            .WithMessage(Messages.FieldRequired);

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage(Messages.PasswordRequired)
            .MinimumLength(6)
            .WithMessage(Messages.PasswordMin);

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty()
            .WithMessage(Messages.PasswordRequired)
            .Equal(x => x.NewPassword)
            .WithMessage(Messages.SamePassword);
    }
}
