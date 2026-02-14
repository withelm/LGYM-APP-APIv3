using FluentValidation;
using LgymApi.Api.DTOs;
using LgymApi.Resources;

namespace LgymApi.Api.Exercises.Validation;

public class ExerciseFormDtoValidator : AbstractValidator<ExerciseFormDto>
{
    public ExerciseFormDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage(Messages.NameIsRequired);

        RuleFor(x => x.BodyPart)
            .NotEmpty()
            .WithMessage(Messages.BodyPartRequired);
    }
}
