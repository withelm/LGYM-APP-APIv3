using FluentValidation;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Exercise.Validation;

public sealed class CreateExerciseMediaValidator : AbstractValidator<CreateExerciseMediaDto>
{
    public CreateExerciseMediaValidator()
    {
        RuleFor(x => x.OperationId).Must(x => Id<ExerciseMediaOperationReference>.TryParse(x, out var id) && !id.IsEmpty).WithMessage(Messages.FieldRequired);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithMessage(Messages.NameIsRequired);
        RuleFor(x => x.Description).MaximumLength(16000).WithMessage(Messages.ExerciseMediaInvalid);
        RuleFor(x => x.BodyPart).IsInEnum().NotEqual(BodyParts.Unknown).WithMessage(Messages.BodyPartRequired);
        RuleFor(x => x.EloFormulaId).Must(x => x is null || global::System.Enum.TryParse<ExerciseEloFormula>(x, true, out var formula) && global::System.Enum.IsDefined(formula)).WithMessage(Messages.InvalidExerciseEloFormula);
        RuleFor(x => x).Must(x => CreateExerciseMediaValidator.ValidSource(x.SourceSha256, x.ContentType, x.SizeBytes, x.File)).WithMessage(Messages.ExerciseMediaInvalid);
    }

    internal static bool ValidSource(string? sha256, string? contentType, long? sizeBytes, IFormFile? file)
    {
        if (sha256 is null && contentType is null && sizeBytes is null) return file is null;
        if (sha256 is null || sha256.Length != 64 || !sha256.All(Uri.IsHexDigit) || contentType is null || !ExerciseMediaLimits.ContentTypes.Contains(contentType) || sizeBytes is not (> 0 and <= ExerciseMediaLimits.MaxBytes)) return false;
        return file is null || file.Length == sizeBytes && file.ContentType == contentType;
    }
}

public sealed class ChangeExerciseMediaValidator : AbstractValidator<ChangeExerciseMediaDto>
{
    public ChangeExerciseMediaValidator()
    {
        RuleFor(x => x.OperationId).Must(x => Id<ExerciseMediaOperationReference>.TryParse(x, out var id) && !id.IsEmpty).WithMessage(Messages.FieldRequired);
        RuleFor(x => x).Must(x => CreateExerciseMediaValidator.ValidSource(x.SourceSha256, x.ContentType, x.SizeBytes, x.File)).WithMessage(Messages.ExerciseMediaInvalid);
        RuleFor(x => x).Must(x => (x.Remove ? 1 : 0) + (x.SourceSha256 is not null ? 1 : 0) + (x.RestoreImage is not null ? 1 : 0) == 1).WithMessage(Messages.ExerciseMediaInvalid);
    }
}
