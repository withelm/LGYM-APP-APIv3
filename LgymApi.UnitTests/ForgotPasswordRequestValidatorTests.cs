using LgymApi.Api.Features.User.Contracts;
using LgymApi.Api.Features.User.Validation;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ForgotPasswordRequestValidatorTests
{
    [Test]
    public void Validate_Passes_WithValidEmail()
    {
        var validator = new ForgotPasswordRequestValidator();
        var request = new ForgotPasswordRequest { Email = "user@example.com" };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_Fails_WhenEmailIsEmpty()
    {
        var validator = new ForgotPasswordRequestValidator();
        var request = new ForgotPasswordRequest { Email = string.Empty };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Some.Property("PropertyName").EqualTo("Email"));
    }

    [Test]
    public void Validate_Fails_WithInvalidEmailFormat()
    {
        var validator = new ForgotPasswordRequestValidator();
        var request = new ForgotPasswordRequest { Email = "not-an-email" };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Some.Property("PropertyName").EqualTo("Email"));
    }

    [Test]
    public void Validate_Passes_WithMaxLengthEmail()
    {
        var validator = new ForgotPasswordRequestValidator();
        var request = new ForgotPasswordRequest { Email = new string('a', 185) + "@example.com" };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_Fails_WithEmailExceedingMaxLength()
    {
        var validator = new ForgotPasswordRequestValidator();
        var request = new ForgotPasswordRequest { Email = new string('a', 195) + "@example.com" };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
    }
}
