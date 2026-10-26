using System.Text.Json;
using FluentAssertions;
using LgymApi.Api.Features.Training.Contracts;
using LgymApi.Api.Features.Training.Validation;
using LgymApi.Resources;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class TrainingByDateRequestDtoValidatorTests
{
    [TestCase("{}")]
    [TestCase("{\"createdAt\":null}")]
    [TestCase("{\"date\":null,\"timeZone\":\"UTC\"}")]
    [TestCase("{\"date\":\"2026-10-22\",\"timeZone\":null}")]
    [TestCase("{\"createdAt\":\"2026-10-22T00:00:00Z\",\"date\":\"2026-10-22\",\"timeZone\":\"UTC\"}")]
    [TestCase("{\"createdAt\":\"2026-10-22T00:00:00Z\",\"timeZone\":\"\"}")]
    [TestCase("{\"createdAt\":\"0001-01-01T00:00:00Z\"}")]
    [TestCase("{\"createdAt\":\"9999-12-31T00:00:00Z\"}")]
    public void InvalidVariant_ReturnsDateError(string json)
    {
        var request = JsonSerializer.Deserialize<TrainingByDateRequestDto>(json)!;
        var result = new TrainingByDateRequestDtoValidator().Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be(Messages.DateRequired);
    }

    [TestCase("Not/AZone")]
    [TestCase("Pacific Standard Time")]
    [TestCase("")]
    [TestCase(" ")]
    public void InvalidZone_DoesNotReportMissingDate(string zone)
    {
        var request = new TrainingByDateRequestDto(date: new DateOnly(2026, 10, 22), timeZone: zone);
        var result = new TrainingByDateRequestDtoValidator().Validate(request);
        result.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be(Messages.InvalidTimeZone);
    }

    [TestCase("{\"createdAt\":\"2026-10-22T12:00:00Z\"}")]
    [TestCase("{\"date\":\"2026-10-22\",\"timeZone\":\"UTC\"}")]
    [TestCase("{\"date\":\"2026-03-29\",\"timeZone\":\"Europe/Warsaw\"}")]
    [TestCase("{\"date\":\"2026-10-25\",\"timeZone\":\"Europe/Warsaw\"}")]
    public void CompleteVariant_IsValid(string json)
    {
        new TrainingByDateRequestDtoValidator().Validate(JsonSerializer.Deserialize<TrainingByDateRequestDto>(json)!)
            .IsValid.Should().BeTrue();
    }
}
