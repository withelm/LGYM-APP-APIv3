using FluentAssertions;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class EloValueObjectTests
{
    [Test]
    public void Constructor_StoresValue_WhenNonNegative()
    {
        var elo = new Elo(1234);

        elo.Value.Should().Be(1234);
    }

    [Test]
    public void Constructor_Throws_WhenNegative()
    {
        var act = new Action(() => _ = new Elo(-1));
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void ImplicitConversions_WorkBothWays()
    {
        Elo elo = 1400;
        int value = elo;

        value.Should().Be(1400);
    }
}
