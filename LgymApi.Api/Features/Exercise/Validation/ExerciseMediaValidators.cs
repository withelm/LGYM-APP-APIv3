using FluentValidation;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.Domain.Enums;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Exercise.Validation;

public sealed class InitiateExerciseMediaValidator : AbstractValidator<InitiateExerciseMediaDto>
{
    public InitiateExerciseMediaValidator()
    {
        RuleFor(x => x.ContentType).NotEmpty().Must(ExerciseMediaLimits.ContentTypes.Contains).WithMessage(Messages.ExerciseMediaInvalid);
        RuleFor(x => x.SizeBytes).InclusiveBetween(1, ExerciseMediaLimits.MaxBytes).WithMessage(Messages.ExerciseMediaInvalid);
        RuleFor(x => x.Sha256).NotEmpty().Matches("^[a-fA-F0-9]{64}$").WithMessage(Messages.ExerciseMediaInvalid);
        RuleFor(x => x.ExerciseId).Must(x => x is null || LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.ExerciseMediaUpload>.TryParse(x, out var id) && !id.IsEmpty).WithMessage(Messages.InvalidId);
    }
}

public sealed class CreateExerciseMediaValidator : AbstractValidator<CreateExerciseMediaDto>
{
    public CreateExerciseMediaValidator()
    {
        RuleFor(x => x.OperationId).Must(x => LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.ValueObjects.ExerciseMediaOperationReference>.TryParse(x, out var id) && !id.IsEmpty).WithMessage(Messages.FieldRequired);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithMessage(Messages.NameIsRequired);
        RuleFor(x => x.BodyPart).IsInEnum().NotEqual(BodyParts.Unknown).WithMessage(Messages.BodyPartRequired);
        RuleFor(x => x.EloFormula).Must(x => x is null || global::System.Enum.TryParse<ExerciseEloFormula>(x.Id, true, out var formula) && global::System.Enum.IsDefined(formula)).WithMessage(Messages.InvalidExerciseEloFormula);
        RuleFor(x => x.UploadReference).Must(x => x is null || LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.ExerciseMediaUpload>.TryParse(x, out var id) && !id.IsEmpty).WithMessage(Messages.InvalidId);
    }
}

public sealed class ChangeExerciseMediaValidator : AbstractValidator<ChangeExerciseMediaDto>
{
    public ChangeExerciseMediaValidator()
    {
        RuleFor(x => x.OperationId).Must(x => LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.ValueObjects.ExerciseMediaOperationReference>.TryParse(x, out var id) && !id.IsEmpty).WithMessage(Messages.FieldRequired);
        RuleFor(x => x.UploadReference).Must(x => x is null || LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.ExerciseMediaUpload>.TryParse(x, out var id) && !id.IsEmpty).WithMessage(Messages.InvalidId);
        RuleFor(x => x).Must(x => (x.Remove ? 1 : 0) + (x.UploadReference is not null ? 1 : 0) + (x.RestoreImage is not null ? 1 : 0) == 1).WithMessage(Messages.ExerciseMediaInvalid);
    }
}
