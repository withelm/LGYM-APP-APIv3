using FluentValidation;
using LgymApi.Api.DTOs;
using LgymApi.Resources;

namespace LgymApi.Api.MainRecords.Validation;

public class MainRecordsFormDtoValidator : AbstractValidator<MainRecordsFormDto>
{
    public MainRecordsFormDtoValidator()
    {
        RuleFor(x => x.ExerciseId)
            .NotEmpty()
            .WithMessage(Messages.ExerciseIdRequired);

        RuleFor(x => x.Unit)
            .NotEmpty()
            .WithMessage(Messages.UnitRequired);

        RuleFor(x => x.Weight)
            .GreaterThan(0)
            .WithMessage(Messages.WeightMustBePositive);

        RuleFor(x => x.Date)
            .NotEmpty()
            .WithMessage(Messages.DateRequired);
    }
}
