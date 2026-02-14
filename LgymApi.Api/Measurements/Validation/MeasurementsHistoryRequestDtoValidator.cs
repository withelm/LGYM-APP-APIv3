using FluentValidation;
using LgymApi.Api.DTOs;
using LgymApi.Resources;

namespace LgymApi.Api.Measurements.Validation;

public class MeasurementsHistoryRequestDtoValidator : AbstractValidator<MeasurementsHistoryRequestDto>
{
    public MeasurementsHistoryRequestDtoValidator()
    {
        RuleFor(x => x.BodyPart)
            .NotEmpty()
            .WithMessage(Messages.BodyPartRequired);
    }
}
