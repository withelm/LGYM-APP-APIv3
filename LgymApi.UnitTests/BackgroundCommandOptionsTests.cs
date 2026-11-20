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
        Assert.That(() => { (options).Validate(); }, Throws.Nothing);
    }

    [Test]
    public void Validate_WithMinimumValidLease_DoesNotThrow()
    {
        var options = new BackgroundCommandOptions { EmailSendLeaseSeconds = 1 };
        Assert.That(() => { (options).Validate(); }, Throws.Nothing);
    }

    [Test]
    public void Validate_WithMaximumValidLease_DoesNotThrow()
    {
        var options = new BackgroundCommandOptions { EmailSendLeaseSeconds = 59 };
        Assert.That(() => { (options).Validate(); }, Throws.Nothing);
    }

    [Test]
    public void Validate_WithZeroLease_ThrowsInvalidOperationException()
    {
        var options = new BackgroundCommandOptions { EmailSendLeaseSeconds = 0 };
        Assert.That(() => { (options).Validate(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*greater than 0*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void Validate_WithNegativeLease_ThrowsInvalidOperationException()
    {
        var options = new BackgroundCommandOptions { EmailSendLeaseSeconds = -5 };
        Assert.That(() => { (options).Validate(); }, Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public void Validate_WithLeaseAtSixty_ThrowsInvalidOperationException()
    {
        var options = new BackgroundCommandOptions { EmailSendLeaseSeconds = 60 };
        Assert.That(() => { (options).Validate(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*less than 60*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void Validate_WithLeaseAboveSixty_ThrowsInvalidOperationException()
    {
        var options = new BackgroundCommandOptions { EmailSendLeaseSeconds = 120 };
        Assert.That(() => { (options).Validate(); }, Throws.InstanceOf<InvalidOperationException>());
    }
}
