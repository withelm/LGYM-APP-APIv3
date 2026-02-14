using FluentValidation;
using LgymApi.Api.DTOs;
using LgymApi.Resources;

namespace LgymApi.Api.Exercises.Validation;

public class LastExerciseScoresRequestDtoValidator : AbstractValidator<LastExerciseScoresRequestDto>
{
    public LastExerciseScoresRequestDtoValidator()
    {
        RuleFor(x => x.Series)
            .GreaterThan(0)
            .WithMessage(Messages.SeriesMustBeGreaterThanZero);

        RuleFor(x => x.ExerciseId)
            .NotEmpty()
            .WithMessage(Messages.ExerciseIdRequired);

        RuleFor(x => x.ExerciseName)
            .NotEmpty()
            .WithMessage(Messages.ExerciseNameRequired);
    }
}
