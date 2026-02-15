using FluentValidation;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Exercise.Validation;

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
