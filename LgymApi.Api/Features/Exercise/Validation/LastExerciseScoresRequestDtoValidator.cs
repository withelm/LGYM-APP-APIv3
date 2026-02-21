using FluentValidation;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Exercise.Validation;

public class LastExerciseScoresRequestDtoValidator : AbstractValidator<LastExerciseScoresRequestDto>
{
    private const int MaxSeriesLimit = 30;

    public LastExerciseScoresRequestDtoValidator()
    {
        RuleFor(x => x.Series)
            .GreaterThan(0)
            .LessThanOrEqualTo(MaxSeriesLimit)
            .WithMessage(Messages.SeriesMustBeGreaterThanZero);

        RuleFor(x => x.ExerciseId)
            .NotEmpty()
            .WithMessage(Messages.ExerciseIdRequired);

        RuleFor(x => x.ExerciseName)
            .NotEmpty()
            .WithMessage(Messages.ExerciseNameRequired);
    }
}
