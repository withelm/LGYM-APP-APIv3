using FluentValidation;
using LgymApi.Api.DTOs;
using LgymApi.Resources;

namespace LgymApi.Api.MainRecords.Validation;

public class RecordOrPossibleRequestDtoValidator : AbstractValidator<RecordOrPossibleRequestDto>
{
    public RecordOrPossibleRequestDtoValidator()
    {
        RuleFor(x => x.ExerciseId)
            .NotEmpty()
            .WithMessage(Messages.ExerciseIdRequired);
    }
}
