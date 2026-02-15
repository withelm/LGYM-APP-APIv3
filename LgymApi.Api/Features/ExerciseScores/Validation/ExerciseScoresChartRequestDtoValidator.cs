using FluentValidation;
using LgymApi.Api.Features.ExerciseScores.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.ExerciseScores.Validation;

public class ExerciseScoresChartRequestDtoValidator : AbstractValidator<ExerciseScoresChartRequestDto>
{
    public ExerciseScoresChartRequestDtoValidator()
    {
        RuleFor(x => x.ExerciseId)
            .NotEmpty()
            .WithMessage(Messages.ExerciseIdRequired);
    }
}
