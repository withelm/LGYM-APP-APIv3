using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Api.Features.Exercise.Validation;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using Microsoft.AspNetCore.Http;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ExerciseMediaRequestValidationTests
{
    [TestCase(0, false)]
    [TestCase(1, true)]
    [TestCase(ExerciseMediaLimits.MaxBytes, true)]
    [TestCase(ExerciseMediaLimits.MaxBytes + 1, false)]
    public void FileOnlyShape_IsBoundedWithoutClientManifest(int size, bool valid)
    {
        var request = new ExerciseMediaUploadDto { File = new FormFile(Stream.Null, 0, size, "file", "untrusted.exe") { Headers = new HeaderDictionary(), ContentType = "application/octet-stream" } };
        Assert.That(new ExerciseMediaUploadValidator().Validate(request).IsValid, Is.EqualTo(valid));
    }

    [Test]
    public void MissingFile_IsRejected() => Assert.That(new ExerciseMediaUploadValidator().Validate(new ExerciseMediaUploadDto()).IsValid, Is.False);
}
