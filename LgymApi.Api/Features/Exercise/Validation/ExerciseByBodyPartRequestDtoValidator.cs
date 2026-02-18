using FluentValidation;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Domain.Enums;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Exercise.Validation;

public sealed class ExerciseByBodyPartRequestDtoValidator : AbstractValidator<ExerciseByBodyPartRequestDto>
{
    public ExerciseByBodyPartRequestDtoValidator()
    {
        RuleFor(x => x.BodyPart)
            .NotEqual(BodyParts.Unknown)
            .WithMessage(Messages.BodyPartRequired);
    }
}
