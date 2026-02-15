using FluentValidation;
using LgymApi.Api.Features.Training.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Training.Validation;

public class TrainingFormDtoValidator : AbstractValidator<TrainingFormDto>
{
    public TrainingFormDtoValidator()
    {
        RuleFor(x => x.TypePlanDayId)
            .NotEmpty()
            .WithMessage(Messages.PlanDayIdRequired);

        RuleFor(x => x.GymId)
            .NotEmpty()
            .WithMessage(Messages.GymIdRequired);

        RuleForEach(x => x.Exercises)
            .SetValidator(new ExerciseScoresTrainingFormDtoValidator());
    }
}
