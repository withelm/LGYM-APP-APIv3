using FluentValidation;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Exercise.Validation;

public sealed class ExerciseMediaUploadValidator : AbstractValidator<ExerciseMediaUploadDto>
{
    public ExerciseMediaUploadValidator()
    {
        RuleFor(x => x.File).NotNull().WithMessage(Messages.ExerciseMediaInvalid);
        When(x => x.File is not null, () =>
            RuleFor(x => x.File.Length).GreaterThan(0).LessThanOrEqualTo(ExerciseMediaLimits.MaxBytes).WithMessage(Messages.ExerciseMediaInvalid));
    }
}
