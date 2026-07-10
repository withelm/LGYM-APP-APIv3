using FluentAssertions;
using LgymApi.Application.Options;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class BackgroundCommandOptionsTests
{
    [Test]
    public void Validate_WithValidLease_DoesNotThrow()
    {
        var options = new BackgroundCommandOptions { EmailSendLeaseSeconds = 30 };
        options.Invoking(o => o.Validate()).Should().NotThrow();
    }

    [Test]
    public void Validate_WithMinimumValidLease_DoesNotThrow()
    {
        var options = new BackgroundCommandOptions { EmailSendLeaseSeconds = 1 };
        options.Invoking(o => o.Validate()).Should().NotThrow();
    }

    [Test]
    public void Validate_WithMaximumValidLease_DoesNotThrow()
    {
        var options = new BackgroundCommandOptions { EmailSendLeaseSeconds = 59 };
        options.Invoking(o => o.Validate()).Should().NotThrow();
    }

    [Test]
    public void Validate_WithZeroLease_ThrowsInvalidOperationException()
    {
        var options = new BackgroundCommandOptions { EmailSendLeaseSeconds = 0 };
        options.Invoking(o => o.Validate())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*greater than 0*");
    }

    [Test]
    public void Validate_WithNegativeLease_ThrowsInvalidOperationException()
    {
        var options = new BackgroundCommandOptions { EmailSendLeaseSeconds = -5 };
        options.Invoking(o => o.Validate())
            .Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void Validate_WithLeaseAtSixty_ThrowsInvalidOperationException()
    {
        var options = new BackgroundCommandOptions { EmailSendLeaseSeconds = 60 };
        options.Invoking(o => o.Validate())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*less than 60*");
    }

    [Test]
    public void Validate_WithLeaseAboveSixty_ThrowsInvalidOperationException()
    {
        var options = new BackgroundCommandOptions { EmailSendLeaseSeconds = 120 };
        options.Invoking(o => o.Validate())
            .Should().Throw<InvalidOperationException>();
    }
}
