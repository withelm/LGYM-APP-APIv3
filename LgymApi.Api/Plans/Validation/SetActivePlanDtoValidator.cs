using FluentValidation;
using LgymApi.Api.DTOs;
using LgymApi.Resources;

namespace LgymApi.Api.Plans.Validation;

public class SetActivePlanDtoValidator : AbstractValidator<SetActivePlanDto>
{
    public SetActivePlanDtoValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage(Messages.FieldRequired);
    }
}
