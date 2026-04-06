using FluentValidation.TestHelper;
using LgymApi.Api.Features.AdminManagement.Contracts;
using LgymApi.Api.Features.AdminManagement.Validation;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class UpdateUserRequestValidatorTests
{
    private readonly UpdateUserRequestValidator _validator = new();

    [Test]
    public void Name_Required_ShouldHaveError()
    {
        var request = new UpdateUserRequest { Name = "", Email = "test@example.com" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Test]
    public void Email_Required_ShouldHaveError()
    {
        var request = new UpdateUserRequest { Name = "Test", Email = "" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Test]
    public void Email_InvalidFormat_ShouldHaveError()
    {
        var request = new UpdateUserRequest { Name = "Test", Email = "not-an-email" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Test]
    public void ValidRequest_ShouldNotHaveErrors()
    {
        var request = new UpdateUserRequest { Name = "Test User", Email = "test@example.com" };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
