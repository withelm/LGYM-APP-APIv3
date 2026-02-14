using FluentValidation;
using LgymApi.Api.DTOs;
using LgymApi.Resources;

namespace LgymApi.Api.Plans.Validation;

public class PlanFormDtoValidator : AbstractValidator<PlanFormDto>
{
    public PlanFormDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage(Messages.NameIsRequired);
    }
}
