using FluentValidation;
using LgymApi.Api.Features.PlanDay.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.PlanDay.Validation;

public class PlanDayFormDtoValidator : AbstractValidator<PlanDayFormDto>
{
    public PlanDayFormDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage(Messages.NameIsRequired);

        RuleForEach(x => x.Exercises)
            .SetValidator(new PlanDayExerciseInputDtoValidator());
    }
}
