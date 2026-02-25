using FluentValidation;
using LgymApi.Api.Features.Role.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Role.Validation;

public sealed class UpsertRoleRequestValidator : AbstractValidator<UpsertRoleRequest>
{
    public UpsertRoleRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage(Messages.FieldRequired);
    }
}
