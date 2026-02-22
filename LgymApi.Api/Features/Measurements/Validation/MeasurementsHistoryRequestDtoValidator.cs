using FluentValidation;
using LgymApi.Api.Features.Measurements.Contracts;
using LgymApi.Domain.Enums;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Measurements.Validation;

public sealed class MeasurementsHistoryRequestDtoValidator : AbstractValidator<MeasurementsHistoryRequestDto>
{
    public MeasurementsHistoryRequestDtoValidator()
    {
        RuleFor(x => x.BodyPart)
            .Must(x => !x.HasValue || x.Value != BodyParts.Unknown)
            .WithMessage(Messages.BodyPartRequired);

        RuleFor(x => x.Unit)
            .Must(x => !x.HasValue || x.Value != HeightUnits.Unknown)
            .WithMessage(Messages.UnitRequired);
    }
}
