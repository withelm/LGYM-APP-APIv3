using FluentValidation;
using LgymApi.Api.Features.Measurements.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Measurements.Validation;

public sealed class MeasurementsBulkFormDtoValidator : AbstractValidator<MeasurementsBulkFormDto>
{
    public MeasurementsBulkFormDtoValidator()
    {
        RuleFor(x => x.Measurements)
            .NotNull()
            .WithMessage(Messages.FieldRequired)
            .Must(items => items is { Count: > 0 })
            .WithMessage(Messages.FieldRequired);

        RuleForEach(x => x.Measurements)
            .SetValidator(new MeasurementFormDtoValidator());
    }
}
