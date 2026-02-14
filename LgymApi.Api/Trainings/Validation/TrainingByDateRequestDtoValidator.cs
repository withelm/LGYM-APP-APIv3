using FluentValidation;
using LgymApi.Api.DTOs;
using LgymApi.Resources;

namespace LgymApi.Api.Trainings.Validation;

public class TrainingByDateRequestDtoValidator : AbstractValidator<TrainingByDateRequestDto>
{
    public TrainingByDateRequestDtoValidator()
    {
        RuleFor(x => x.CreatedAt)
            .NotEmpty()
            .WithMessage(Messages.DateRequired);
    }
}
