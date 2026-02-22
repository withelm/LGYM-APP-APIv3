using FluentValidation;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Application.Features.Exercise;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Exercise.Validation;

public class LastExerciseScoresRequestDtoValidator : AbstractValidator<LastExerciseScoresRequestDto>
{
    public LastExerciseScoresRequestDtoValidator()
    {
        RuleFor(x => x.Series)
            .GreaterThan(0)
            .WithMessage(Messages.SeriesMustBeGreaterThanZero)
            .LessThanOrEqualTo(ExerciseLimits.MaxSeries)
            .WithMessage(Messages.SeriesMustBeBetweenOneAndThirty);

        RuleFor(x => x.ExerciseId)
            .NotEmpty()
            .WithMessage(Messages.ExerciseIdRequired);

        RuleFor(x => x.ExerciseName)
            .NotEmpty()
            .WithMessage(Messages.ExerciseNameRequired);
    }
}
