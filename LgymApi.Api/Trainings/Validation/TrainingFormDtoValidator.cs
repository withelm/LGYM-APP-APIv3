using FluentValidation;
using LgymApi.Api.DTOs;
using LgymApi.Resources;

namespace LgymApi.Api.Trainings.Validation;

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
