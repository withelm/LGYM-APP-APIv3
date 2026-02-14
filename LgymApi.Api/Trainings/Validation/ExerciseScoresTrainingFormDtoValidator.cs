using FluentValidation;
using LgymApi.Api.DTOs;
using LgymApi.Resources;

namespace LgymApi.Api.Trainings.Validation;

public class ExerciseScoresTrainingFormDtoValidator : AbstractValidator<ExerciseScoresTrainingFormDto>
{
    public ExerciseScoresTrainingFormDtoValidator()
    {
        RuleFor(x => x.ExerciseId)
            .NotEmpty()
            .WithMessage(Messages.ExerciseIdRequired);

        RuleFor(x => x.Unit)
            .NotEmpty()
            .WithMessage(Messages.UnitRequired);

        RuleFor(x => x.Series)
            .GreaterThan(0)
            .WithMessage(Messages.SeriesMustBeGreaterThanZero);

        RuleFor(x => x.Reps)
            .GreaterThanOrEqualTo(0)
            .WithMessage(Messages.RepsMustBePositive);

        RuleFor(x => x.Weight)
            .GreaterThanOrEqualTo(0)
            .WithMessage(Messages.WeightMustBePositive);
    }
}
