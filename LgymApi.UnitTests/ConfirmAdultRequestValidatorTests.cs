using FluentAssertions;
using LgymApi.Api.Features.Account.Contracts;
using LgymApi.Api.Features.Account.Validation;
using LgymApi.Resources;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ConfirmAdultRequestValidatorTests
{
    [TestCase(null)]
    [TestCase(false)]
    public void Validate_Fails_WhenAdultConfirmationIsMissingOrFalse(bool? adultConfirmed)
    {
        var validator = new ConfirmAdultRequestValidator();
        var request = new ConfirmAdultRequest { AdultConfirmed = adultConfirmed };

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(Messages.AdultConfirmationRequired);
    }

    [Test]
    public void Validate_Passes_WhenAdultConfirmationIsTrue()
    {
        var validator = new ConfirmAdultRequestValidator();
        var request = new ConfirmAdultRequest { AdultConfirmed = true };

        var result = validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }
}
