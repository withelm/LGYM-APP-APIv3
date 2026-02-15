using FluentValidation;
using LgymApi.Api.Features.Plan.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Plan.Validation;

public class PlanFormDtoValidator : AbstractValidator<PlanFormDto>
{
    public PlanFormDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage(Messages.NameIsRequired);
    }
}
