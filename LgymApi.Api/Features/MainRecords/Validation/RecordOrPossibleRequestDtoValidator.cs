using FluentValidation;
using LgymApi.Api.Features.MainRecords.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.MainRecords.Validation;

public class RecordOrPossibleRequestDtoValidator : AbstractValidator<RecordOrPossibleRequestDto>
{
    public RecordOrPossibleRequestDtoValidator()
    {
        RuleFor(x => x.ExerciseId)
            .NotEmpty()
            .WithMessage(Messages.ExerciseIdRequired);
    }
}
