using FluentValidation;
using LgymApi.Api.DTOs;
using LgymApi.Resources;

namespace LgymApi.Api.Measurements.Validation;

public class MeasurementFormDtoValidator : AbstractValidator<MeasurementFormDto>
{
    public MeasurementFormDtoValidator()
    {
        RuleFor(x => x.BodyPart)
            .NotEmpty()
            .WithMessage(Messages.BodyPartRequired);

        RuleFor(x => x.Unit)
            .NotEmpty()
            .WithMessage(Messages.UnitRequired);

        RuleFor(x => x.Value)
            .GreaterThan(0)
            .WithMessage(Messages.ValueMustBePositive);
    }
}
