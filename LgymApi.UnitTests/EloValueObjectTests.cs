using LgymApi.Domain.ValueObjects;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class EloValueObjectTests
{
    [Test]
    public void Constructor_StoresValue_WhenNonNegative()
    {
        var elo = new Elo(1234);

        Assert.That(elo.Value, Is.EqualTo(1234));
    }

    [Test]
    public void Constructor_Throws_WhenNegative()
    {
        var act = new Action(() => _ = new Elo(-1));
        Assert.That(() => { act(); }, Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void ImplicitConversions_WorkBothWays()
    {
        Elo elo = 1400;
        int value = elo;

        Assert.That(value, Is.EqualTo(1400));
    }
}
