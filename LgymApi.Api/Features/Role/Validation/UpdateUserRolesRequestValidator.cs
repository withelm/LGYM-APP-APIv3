using FluentValidation;
using LgymApi.Api.Features.Role.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Role.Validation;

public sealed class UpdateUserRolesRequestValidator : AbstractValidator<UpdateUserRolesRequest>
{
    public UpdateUserRolesRequestValidator()
    {
        RuleFor(x => x.Roles)
            .NotNull()
            .WithMessage(Messages.FieldRequired);
    }
}
