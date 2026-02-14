using FluentValidation;
using LgymApi.Api.DTOs;
using LgymApi.Resources;

namespace LgymApi.Api.ExerciseScores.Validation;

public class ExerciseScoresChartRequestDtoValidator : AbstractValidator<ExerciseScoresChartRequestDto>
{
    public ExerciseScoresChartRequestDtoValidator()
    {
        RuleFor(x => x.ExerciseId)
            .NotEmpty()
            .WithMessage(Messages.ExerciseIdRequired);
    }
}
