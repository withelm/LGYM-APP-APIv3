using FluentValidation;
using LgymApi.Api.Features.Measurements.Contracts;
using LgymApi.Application.Features.Measurements;
using LgymApi.Domain.Enums;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Measurements.Validation;

public sealed class MeasurementsHistoryRequestDtoValidator : AbstractValidator<MeasurementsHistoryRequestDto>
{
    public MeasurementsHistoryRequestDtoValidator()
    {
        RuleFor(x => x.BodyPart)
            .Must(x => !x.HasValue || System.Enum.IsDefined(x.Value))
            .WithMessage(Messages.BodyPartRequired)
            .Must(x => !x.HasValue || x.Value != BodyParts.Unknown)
            .WithMessage(Messages.BodyPartRequired);

        RuleFor(x => x.Unit)
            .Must(x => !x.HasValue || System.Enum.IsDefined(x.Value))
            .WithMessage(Messages.UnitRequired)
            .Must(x => !x.HasValue || x.Value != MeasurementUnits.Unknown)
            .WithMessage(Messages.UnitRequired)
            .Must((dto, unit) => !unit.HasValue || (dto.BodyPart.HasValue && MeasurementUnitResolver.IsUnitAllowedForBodyPart(dto.BodyPart.Value, unit.Value)))
            .WithMessage(Messages.UnitRequired);
    }
}
