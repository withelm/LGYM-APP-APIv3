using FluentValidation;
using LgymApi.Api.Features.ExerciseScores.Contracts;
using LgymApi.Domain.Enums;
using LgymApi.Resources;

namespace LgymApi.Api.Features.ExerciseScores.Validation;

public class ExerciseScoresFormDtoValidator : AbstractValidator<ExerciseScoresFormDto>
{
    public ExerciseScoresFormDtoValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage(Messages.UserIdRequired);

        RuleFor(x => x.TrainingId)
            .NotEmpty()
            .WithMessage(Messages.TrainingIdRequired);

        RuleFor(x => x.Date)
            .NotEmpty()
            .WithMessage(Messages.DateRequired);

        RuleFor(x => x.ExerciseId)
            .NotEmpty()
            .WithMessage(Messages.ExerciseIdRequired);

        RuleFor(x => x.Unit)
            .IsInEnum()
            .NotEqual(WeightUnits.Unknown)
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
