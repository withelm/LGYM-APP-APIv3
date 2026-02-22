using FluentValidation;
using LgymApi.Api.Features.Measurements.Contracts;
using LgymApi.Domain.Enums;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Measurements.Validation;

public sealed class MeasurementTrendRequestDtoValidator : AbstractValidator<MeasurementTrendRequestDto>
{
    public MeasurementTrendRequestDtoValidator()
    {
        RuleFor(x => x.BodyPart)
            .IsInEnum()
            .NotEqual(BodyParts.Unknown)
            .WithMessage(Messages.BodyPartRequired);

        RuleFor(x => x.Unit)
            .IsInEnum()
            .NotEqual(HeightUnits.Unknown)
            .WithMessage(Messages.UnitRequired);
    }
}
