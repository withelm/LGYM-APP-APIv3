using FluentValidation;
using LgymApi.Api.DTOs;
using LgymApi.Resources;

namespace LgymApi.Api.Exercises.Validation;

public class ExerciseTranslationDtoValidator : AbstractValidator<ExerciseTranslationDto>
{
    public ExerciseTranslationDtoValidator()
    {
        RuleFor(x => x.ExerciseId)
            .NotEmpty()
            .WithMessage(Messages.ExerciseIdRequired);

        RuleFor(x => x.Culture)
            .NotEmpty()
            .WithMessage(Messages.CultureRequired);

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage(Messages.NameIsRequired);
    }
}
