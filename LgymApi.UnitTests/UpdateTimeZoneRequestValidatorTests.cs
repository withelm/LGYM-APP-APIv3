using LgymApi.Api.Features.User.Contracts;
using LgymApi.Api.Features.User.Validation;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class UpdateTimeZoneRequestValidatorTests
{
    [Test]
    public void Validate_Fails_WhenTimeZoneIdIsEmpty()
    {
        var validator = new UpdateTimeZoneRequestValidator();
        var request = new UpdateTimeZoneRequest { TimeZoneId = string.Empty };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void Validate_Fails_WhenTimeZoneIdIsInvalid()
    {
        var validator = new UpdateTimeZoneRequestValidator();
        var request = new UpdateTimeZoneRequest { TimeZoneId = "Not/ARealTimeZone" };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void Validate_Passes_WhenTimeZoneIdIsValid()
    {
        var validator = new UpdateTimeZoneRequestValidator();
        var request = new UpdateTimeZoneRequest { TimeZoneId = "Europe/Warsaw" };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.True);
    }
}
