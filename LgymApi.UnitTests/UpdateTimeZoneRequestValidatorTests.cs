using LgymApi.Api.Features.User.Contracts;
using LgymApi.Api.Features.User.Validation;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class UpdateTimeZoneRequestValidatorTests
{
    [Test]
    public void Validate_Fails_WhenPreferredTimeZoneIsEmpty()
    {
        var validator = new UpdateTimeZoneRequestValidator();
        var request = new UpdateTimeZoneRequest { PreferredTimeZone = string.Empty };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void Validate_Fails_WhenPreferredTimeZoneIsInvalid()
    {
        var validator = new UpdateTimeZoneRequestValidator();
        var request = new UpdateTimeZoneRequest { PreferredTimeZone = "Not/ARealTimeZone" };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void Validate_Passes_WhenPreferredTimeZoneIsValid()
    {
        var validator = new UpdateTimeZoneRequestValidator();
        var request = new UpdateTimeZoneRequest { PreferredTimeZone = "Europe/Warsaw" };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.True);
    }
}
