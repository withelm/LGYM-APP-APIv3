using FluentAssertions;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Api.Features.Exercise.Validation;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ExerciseMediaRequestValidationTests
{
    [TestCase("invalid-reference")]
    [TestCase("00000000-0000-0000-0000-000000000000")]
    public void Initiation_RejectsInvalidTargetIdentity(string exerciseId)
    {
        var request = new InitiateExerciseMediaDto { ContentType = "image/gif", SizeBytes = 123, Sha256 = new string('a', 64), ExerciseId = exerciseId };
        var result = new InitiateExerciseMediaValidator().Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(request.ExerciseId));
    }

    [TestCase("image/gif")]
    [TestCase("image/png")]
    [TestCase("image/jpeg")]
    [TestCase("image/webp")]
    public void Initiation_AcceptsSupportedOriginalDeclarationsWithTarget(string contentType)
    {
        var request = new InitiateExerciseMediaDto { ContentType = contentType, SizeBytes = 123, Sha256 = new string('A', 64), ExerciseId = Id<Exercise>.New().ToString() };
        new InitiateExerciseMediaValidator().Validate(request).IsValid.Should().BeTrue();
    }

    [TestCase("not-a-formula")]
    [TestCase("2147483647")]
    public void Create_RejectsUnknownFormulaNamesAndUndefinedNumericValues(string formula)
    {
        var request = new CreateExerciseMediaDto { OperationId = Id<ExerciseMediaOperationReference>.New().ToString(), Name = "Exercise", BodyPart = BodyParts.Chest, EloFormula = new LookupItemVm { Id = formula } };
        var result = new CreateExerciseMediaValidator().Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(request.EloFormula));
    }

    [TestCase(0, false)]
    [TestCase(1, true)]
    [TestCase(2, true)]
    [TestCase(3, false)]
    [TestCase(4, true)]
    [TestCase(5, false)]
    [TestCase(6, false)]
    [TestCase(7, false)]
    public void Change_RequiresExactlyOneUploadRemovalOrRetainedRestore(int actions, bool valid)
    {
        var request = new ChangeExerciseMediaDto
        {
            OperationId = Id<ExerciseMediaOperationReference>.New().ToString(),
            ExpectedImage = null,
            Remove = (actions & 1) != 0,
            UploadReference = (actions & 2) != 0 ? Id<ExerciseMediaUpload>.New().ToString() : null,
            RestoreImage = (actions & 4) != 0 ? "https://exercise.example.invalid/retained.gif" : null
        };
        new ChangeExerciseMediaValidator().Validate(request).IsValid.Should().Be(valid);
    }

    [TestCase("invalid-reference")]
    [TestCase("00000000-0000-0000-0000-000000000000")]
    public void CreateAndChange_RejectInvalidOperationAndUploadReferences(string reference)
    {
        var create = new CreateExerciseMediaDto { OperationId = reference, Name = "Exercise", BodyPart = BodyParts.Chest, UploadReference = reference };
        var createErrors = new CreateExerciseMediaValidator().Validate(create).Errors.Select(error => error.PropertyName);
        createErrors.Should().Contain(nameof(create.OperationId)).And.Contain(nameof(create.UploadReference));
        var change = new ChangeExerciseMediaDto { OperationId = reference, UploadReference = reference, ExpectedImage = null };
        var changeErrors = new ChangeExerciseMediaValidator().Validate(change).Errors.Select(error => error.PropertyName);
        changeErrors.Should().Contain(nameof(change.OperationId)).And.Contain(nameof(change.UploadReference));
    }
}
