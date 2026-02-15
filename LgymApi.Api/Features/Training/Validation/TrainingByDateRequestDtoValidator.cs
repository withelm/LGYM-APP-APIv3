using FluentValidation;
using LgymApi.Api.Features.Training.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Training.Validation;

public class TrainingByDateRequestDtoValidator : AbstractValidator<TrainingByDateRequestDto>
{
    public TrainingByDateRequestDtoValidator()
    {
        RuleFor(x => x.CreatedAt)
            .NotEmpty()
            .WithMessage(Messages.DateRequired);
    }
}
