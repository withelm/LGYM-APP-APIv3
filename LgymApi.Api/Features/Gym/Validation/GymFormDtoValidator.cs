using FluentValidation;
using LgymApi.Api.Features.Gym.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Gym.Validation;

public class GymFormDtoValidator : AbstractValidator<GymFormDto>
{
    public GymFormDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage(Messages.NameIsRequired);
    }
}
