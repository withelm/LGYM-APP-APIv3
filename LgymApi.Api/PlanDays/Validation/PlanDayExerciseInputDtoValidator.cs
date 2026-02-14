using FluentValidation;
using LgymApi.Api.DTOs;
using LgymApi.Resources;

namespace LgymApi.Api.PlanDays.Validation;

public class PlanDayExerciseInputDtoValidator : AbstractValidator<PlanDayExerciseInputDto>
{
    public PlanDayExerciseInputDtoValidator()
    {
        RuleFor(x => x.ExerciseId)
            .NotEmpty()
            .WithMessage(Messages.ExerciseIdRequired);

        RuleFor(x => x.Series)
            .GreaterThan(0)
            .WithMessage(Messages.SeriesMustBeGreaterThanZero);

        RuleFor(x => x.Reps)
            .NotEmpty()
            .WithMessage(Messages.RepsRequired);
    }
}
