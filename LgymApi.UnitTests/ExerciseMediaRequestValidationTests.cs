using FluentAssertions;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Api.Features.Exercise.Validation;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ExerciseMediaRequestValidationTests
{
    private static CreateExerciseMediaDto Create() => new()
    {
        OperationId = Id<ExerciseMediaOperationReference>.New().ToString(),
        Name = "Technical exercise fixture", BodyPart = BodyParts.Chest
    };

    [TestCase(200, true)]
    [TestCase(201, false)]
    [TestCase(0, false)]
    public void Create_NameBoundaryIsEnforcedBeforeAnyPublication(int length, bool valid)
    {
        var request = Create();
        request.Name = new string('a', length);
        new CreateExerciseMediaValidator().Validate(request).IsValid.Should().Be(valid);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Create_NameLengthUsesUtf16Units(bool overLimit)
    {
        var request = Create();
        request.Name = string.Concat(Enumerable.Repeat("\U0001F3CB", 100)) + (overLimit ? "a" : "");
        request.Name.Length.Should().Be(overLimit ? 201 : 200);
        new CreateExerciseMediaValidator().Validate(request).IsValid.Should().Be(!overLimit);
    }

    [TestCase(16000, true)]
    [TestCase(16001, false)]
    public void Create_DescriptionHasBoundedFormLength(int length, bool valid)
    {
        var request = Create();
        request.Description = new string('d', length);
        new CreateExerciseMediaValidator().Validate(request).IsValid.Should().Be(valid);
    }

    [TestCase("Standard")]
    [TestCase("StrengthWeighted")]
    [TestCase("volumeweighted")]
    [TestCase("0")]
    public void Create_AcceptsExistingFormulaLookupIds(string formula)
    {
        var request = Create();
        request.EloFormulaId = formula;
        new CreateExerciseMediaValidator().Validate(request).IsValid.Should().BeTrue();
    }

    [TestCase("not-a-formula")]
    [TestCase("2147483647")]
    public void Create_RejectsUnknownFormulaLookupIds(string formula)
    {
        var request = Create();
        request.EloFormulaId = formula;
        new CreateExerciseMediaValidator().Validate(request).Errors.Should().Contain(error => error.PropertyName == nameof(request.EloFormulaId));
    }

    [TestCase(BodyParts.Unknown)]
    [TestCase((BodyParts)int.MaxValue)]
    public void Create_RejectsUnknownBodyPart(BodyParts bodyPart)
    {
        var request = Create();
        request.BodyPart = bodyPart;
        new CreateExerciseMediaValidator().Validate(request).Errors.Should().Contain(error => error.PropertyName == nameof(request.BodyPart));
    }

    [TestCase("image/gif")]
    [TestCase("image/png")]
    [TestCase("image/jpeg")]
    [TestCase("image/webp")]
    public void Create_AcceptsOriginalFileManifestAndExactManifestWithoutFileForReplay(string contentType)
    {
        var request = Create();
        request.SourceSha256 = new string('A', 64);
        request.ContentType = contentType;
        request.SizeBytes = 3;
        request.File = File(contentType, 3);
        new CreateExerciseMediaValidator().Validate(request).IsValid.Should().BeTrue();
        request.File = null;
        new CreateExerciseMediaValidator().Validate(request).IsValid.Should().BeTrue();
    }

    [TestCase(0, true)]
    [TestCase(1, false)]
    [TestCase(2, false)]
    [TestCase(3, false)]
    [TestCase(4, false)]
    [TestCase(5, false)]
    [TestCase(6, false)]
    [TestCase(7, true)]
    public void Create_RequiresEveryManifestScalarOrNone(int present, bool valid)
    {
        var request = Create();
        request.SourceSha256 = (present & 1) != 0 ? new string('a', 64) : null;
        request.ContentType = (present & 2) != 0 ? "image/gif" : null;
        request.SizeBytes = (present & 4) != 0 ? 3 : null;
        new CreateExerciseMediaValidator().Validate(request).IsValid.Should().Be(valid);
    }

    [Test]
    public void FileWithoutManifest_IsRejectedInsteadOfBecomingNoMediaCreate()
    {
        var request = Create();
        request.File = File("image/gif", 3);
        new CreateExerciseMediaValidator().Validate(request).IsValid.Should().BeFalse();
    }

    [TestCase(63, 'a')]
    [TestCase(65, 'a')]
    [TestCase(64, 'z')]
    public void Source_RejectsIncompleteOrNonHexChecksum(int length, char character)
    {
        var request = WithSource();
        request.SourceSha256 = new string(character, length);
        new CreateExerciseMediaValidator().Validate(request).IsValid.Should().BeFalse();
    }

    [TestCase("image/heic")]
    [TestCase("image/svg+xml")]
    [TestCase("image/jpg")]
    [TestCase("IMAGE/GIF")]
    [TestCase("application/octet-stream")]
    public void Source_RejectsUnsupportedOrInexactMime(string contentType)
    {
        var request = WithSource();
        request.ContentType = contentType;
        new CreateExerciseMediaValidator().Validate(request).IsValid.Should().BeFalse();
    }

    [TestCase(0, false)]
    [TestCase(-1, false)]
    [TestCase(1, true)]
    [TestCase(ExerciseMediaLimits.MaxBytes, true)]
    [TestCase(ExerciseMediaLimits.MaxBytes + 1, false)]
    public void Source_RequiresPositiveSizeWithinFiveMiB(int size, bool valid)
    {
        var request = WithSource();
        request.SizeBytes = size;
        new CreateExerciseMediaValidator().Validate(request).IsValid.Should().Be(valid);
    }

    [TestCase("mime")]
    [TestCase("size")]
    public void MultipartFile_MustAgreeWithManifest(string mismatch)
    {
        var request = WithSource();
        request.File = mismatch == "mime" ? File("image/png", 3) : File("image/gif", 4);
        new CreateExerciseMediaValidator().Validate(request).IsValid.Should().BeFalse();
    }

    [TestCase(0, false)]
    [TestCase(1, true)]
    [TestCase(2, true)]
    [TestCase(3, false)]
    [TestCase(4, true)]
    [TestCase(5, false)]
    [TestCase(6, false)]
    [TestCase(7, false)]
    public void Change_RequiresExactlyOneReplacementRemovalOrRetainedRestore(int actions, bool valid)
    {
        var request = new ChangeExerciseMediaDto
        {
            OperationId = Id<ExerciseMediaOperationReference>.New().ToString(),
            // The HTTP binder separately enforces field presence; explicit empty means null here.
            ExpectedImage = null,
            SourceSha256 = (actions & 1) != 0 ? new string('a', 64) : null,
            ContentType = (actions & 1) != 0 ? "image/gif" : null,
            SizeBytes = (actions & 1) != 0 ? 3 : null,
            File = (actions & 1) != 0 ? File("image/gif", 3) : null,
            Remove = (actions & 2) != 0,
            RestoreImage = (actions & 4) != 0 ? "https://exercises-dev.owner.test/retained.gif" : null
        };
        new ChangeExerciseMediaValidator().Validate(request).IsValid.Should().Be(valid);
    }

    [Test]
    public void Change_AllowsExactReplacementManifestWithoutFileForKnownOperationReplay()
    {
        var request = new ChangeExerciseMediaDto
        {
            OperationId = Id<ExerciseMediaOperationReference>.New().ToString(), ExpectedImage = "https://legacy.owner.test/old.gif",
            SourceSha256 = new string('a', 64), ContentType = "image/gif", SizeBytes = 3
        };
        new ChangeExerciseMediaValidator().Validate(request).IsValid.Should().BeTrue();
    }

    [TestCase("invalid-operation")]
    [TestCase("00000000-0000-0000-0000-000000000000")]
    public void CreateAndChange_RequireNonEmptyTypedOperationIdentity(string operationId)
    {
        var create = Create();
        create.OperationId = operationId;
        new CreateExerciseMediaValidator().Validate(create).Errors.Should().Contain(error => error.PropertyName == nameof(create.OperationId));
        var change = new ChangeExerciseMediaDto { OperationId = operationId, ExpectedImage = null, Remove = true };
        new ChangeExerciseMediaValidator().Validate(change).Errors.Should().Contain(error => error.PropertyName == nameof(change.OperationId));
    }

    private static CreateExerciseMediaDto WithSource()
    {
        var request = Create();
        request.SourceSha256 = new string('a', 64);
        request.ContentType = "image/gif";
        request.SizeBytes = 3;
        return request;
    }

    private static IFormFile File(string contentType, int size) => new FormFile(new MemoryStream(new byte[size]), 0, size, "file", "technical-fixture")
    {
        Headers = new HeaderDictionary(), ContentType = contentType
    };
}
