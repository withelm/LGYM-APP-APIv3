using LgymApi.Api.Features.User.Contracts;
using LgymApi.Api.Features.User.Validation;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ResetPasswordRequestValidatorTests
{
    [Test]
    public void Validate_Passes_WithValidRequest()
    {
        var validator = new ResetPasswordRequestValidator();
        var request = new ResetPasswordRequest
        {
            Token = "valid-token-hash",
            NewPassword = "Password123",
            ConfirmPassword = "Password123"
        };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_Fails_WhenTokenIsEmpty()
    {
        var validator = new ResetPasswordRequestValidator();
        var request = new ResetPasswordRequest
        {
            Token = string.Empty,
            NewPassword = "Password123",
            ConfirmPassword = "Password123"
        };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Some.Property("PropertyName").EqualTo("Token"));
    }

    [Test]
    public void Validate_Fails_WhenNewPasswordIsEmpty()
    {
        var validator = new ResetPasswordRequestValidator();
        var request = new ResetPasswordRequest
        {
            Token = "valid-token",
            NewPassword = string.Empty,
            ConfirmPassword = "Password123"
        };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Some.Property("PropertyName").EqualTo("NewPassword"));
    }

    [Test]
    public void Validate_Fails_WhenNewPasswordIsTooShort()
    {
        var validator = new ResetPasswordRequestValidator();
        var request = new ResetPasswordRequest
        {
            Token = "valid-token",
            NewPassword = "pass",
            ConfirmPassword = "pass"
        };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Some.Property("PropertyName").EqualTo("NewPassword"));
    }

    [Test]
    public void Validate_Fails_WhenConfirmPasswordIsEmpty()
    {
        var validator = new ResetPasswordRequestValidator();
        var request = new ResetPasswordRequest
        {
            Token = "valid-token",
            NewPassword = "Password123",
            ConfirmPassword = string.Empty
        };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Some.Property("PropertyName").EqualTo("ConfirmPassword"));
    }

    [Test]
    public void Validate_Fails_WhenConfirmPasswordDoesNotMatch()
    {
        var validator = new ResetPasswordRequestValidator();
        var request = new ResetPasswordRequest
        {
            Token = "valid-token",
            NewPassword = "Password123",
            ConfirmPassword = "DifferentPassword"
        };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Some.Property("PropertyName").EqualTo("ConfirmPassword"));
    }

    [Test]
    public void Validate_Passes_WithExactlyMinimumPasswordLength()
    {
        var validator = new ResetPasswordRequestValidator();
        var request = new ResetPasswordRequest
        {
            Token = "valid-token",
            NewPassword = "123456",
            ConfirmPassword = "123456"
        };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_Fails_WithPasswordOneBelowMinimumLength()
    {
        var validator = new ResetPasswordRequestValidator();
        var request = new ResetPasswordRequest
        {
            Token = "valid-token",
            NewPassword = "12345",
            ConfirmPassword = "12345"
        };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Some.Property("PropertyName").EqualTo("NewPassword"));
    }
}
