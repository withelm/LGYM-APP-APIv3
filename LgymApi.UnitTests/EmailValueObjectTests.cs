using FluentAssertions;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class EmailValueObjectTests
{
    [Test]
    public void Constructor_NormalizesTrimAndCase()
    {
        var email = new Email("  USER@Example.COM ");

        email.Value.Should().Be("user@example.com");
    }

    [TestCase("not-an-email")]
    public void Constructor_Throws_ForInvalidInput(string value)
    {
        var action = () => _ = new Email(value);
        action.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Constructor_Allows_EmptyInput_ForDeferredEmailFlows()
    {
        var email = new Email("   ");

        email.IsEmpty.Should().BeTrue();
        email.Value.Should().Be(string.Empty);
    }

    [Test]
    public void ImplicitConversions_WorkBothWays()
    {
        Email email = "user@example.com";
        string value = email;

        value.Should().Be("user@example.com");
    }
}
