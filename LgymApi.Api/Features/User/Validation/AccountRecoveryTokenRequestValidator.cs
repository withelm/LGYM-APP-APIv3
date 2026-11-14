using FluentValidation;
using LgymApi.Api.Features.User.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.User.Validation;

public sealed class AccountRecoveryTokenRequestValidator : AbstractValidator<AccountRecoveryTokenRequest>
{
    public AccountRecoveryTokenRequestValidator()
    {
        RuleFor(request => request.RecoveryToken).NotEmpty().WithMessage(Messages.InvalidToken)
            .Length(64).WithMessage(Messages.InvalidToken)
            .Matches("^[0-9A-Fa-f]{64}$").WithMessage(Messages.InvalidToken);
    }
}
