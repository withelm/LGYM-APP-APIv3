using FluentValidation;
using LgymApi.Api.DTOs;
using LgymApi.Resources;

namespace LgymApi.Api.Gyms.Validation;

public class GymFormDtoValidator : AbstractValidator<GymFormDto>
{
    public GymFormDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage(Messages.NameIsRequired);
    }
}
