using FluentValidation;
using LgymApi.Api.Features.AdminManagement.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.AdminManagement.Validation;

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage(Messages.NameIsRequired);

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage(Messages.FieldRequired)
            .EmailAddress()
            .WithMessage(Messages.EmailInvalid);
    }
}
