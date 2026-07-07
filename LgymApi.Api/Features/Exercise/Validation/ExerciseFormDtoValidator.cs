using FluentValidation;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Domain.Enums;
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
            .IsInEnum()
            .NotEqual(BodyParts.Unknown)
            .WithMessage(Messages.BodyPartRequired);
    }
}

public sealed class ExerciseExtendedFormDtoValidator : AbstractValidator<ExerciseExtendedFormDto>
{
    public ExerciseExtendedFormDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage(Messages.NameIsRequired);

        RuleFor(x => x.BodyPart)
            .IsInEnum()
            .NotEqual(BodyParts.Unknown)
            .WithMessage(Messages.BodyPartRequired);

        RuleFor(x => x.EloFormula)
            .Must(value => string.IsNullOrWhiteSpace(value) || global::System.Enum.TryParse<ExerciseEloFormula>(value, ignoreCase: true, out _))
            .WithMessage(Messages.InvalidExerciseEloFormula);
    }
}
