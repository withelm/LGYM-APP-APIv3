using FluentValidation;
using LgymApi.Api.Features.MainRecords.Contracts;
using LgymApi.Domain.Enums;
using LgymApi.Resources;

namespace LgymApi.Api.Features.MainRecords.Validation;

public class MainRecordsFormDtoValidator : AbstractValidator<MainRecordsFormDto>
{
    public MainRecordsFormDtoValidator()
    {
        RuleFor(x => x.ExerciseId)
            .NotEmpty()
            .WithMessage(Messages.ExerciseIdRequired);

        RuleFor(x => x.Unit)
            .NotEqual(WeightUnits.Unknown)
            .WithMessage(Messages.UnitRequired);

        RuleFor(x => x.Weight)
            .GreaterThan(0)
            .WithMessage(Messages.WeightMustBePositive);

        RuleFor(x => x.Date)
            .NotEmpty()
            .WithMessage(Messages.DateRequired);
    }
}
