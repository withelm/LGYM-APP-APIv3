using FluentValidation;
using LgymApi.Api.Features.Measurements.Contracts;
using LgymApi.Domain.Enums;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Measurements.Validation;

public class MeasurementFormDtoValidator : AbstractValidator<MeasurementFormDto>
{
    public MeasurementFormDtoValidator()
    {
        RuleFor(x => x.BodyPart)
            .NotEqual(BodyParts.Unknown)
            .WithMessage(Messages.BodyPartRequired);

        RuleFor(x => x.Unit)
            .NotEqual(HeightUnits.Unknown)
            .WithMessage(Messages.UnitRequired);

        RuleFor(x => x.Value)
            .GreaterThan(0)
            .WithMessage(Messages.ValueMustBePositive);
    }
}
