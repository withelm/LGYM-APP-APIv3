using FluentValidation;
using LgymApi.Api.Features.Plan.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Plan.Validation;

public class SetActivePlanDtoValidator : AbstractValidator<SetActivePlanDto>
{
    public SetActivePlanDtoValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage(Messages.FieldRequired);
    }
}
