using LgymApi.Domain.ValueObjects;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class EmailValueObjectTests
{
    [Test]
    public void Constructor_NormalizesTrimAndCase()
    {
        var email = new Email("  USER@Example.COM ");

        Assert.That(email.Value, Is.EqualTo("user@example.com"));
    }

    [TestCase("not-an-email")]
    public void Constructor_Throws_ForInvalidInput(string value)
    {
        Assert.Throws<ArgumentException>(() => _ = new Email(value));
    }

    [Test]
    public void Constructor_Allows_EmptyInput_ForDeferredEmailFlows()
    {
        var email = new Email("   ");

        Assert.That(email.IsEmpty, Is.True);
        Assert.That(email.Value, Is.EqualTo(string.Empty));
    }

    [Test]
    public void ImplicitConversions_WorkBothWays()
    {
        Email email = "user@example.com";
        string value = email;

        Assert.That(value, Is.EqualTo("user@example.com"));
    }
}
