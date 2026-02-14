using FluentValidation;
using LgymApi.Api.DTOs;
using LgymApi.Resources;

namespace LgymApi.Api.PlanDays.Validation;

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
