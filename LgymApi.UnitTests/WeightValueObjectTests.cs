using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class WeightValueObjectTests
{
    [Test]
    public void ConvertTo_ReturnsSame_WhenTargetMatches()
    {
        var weight = new Weight(100, WeightUnits.Kilograms);

        var converted = weight.ConvertTo(WeightUnits.Kilograms, (_, _, _) => throw new InvalidOperationException());

        Assert.That(converted, Is.EqualTo(weight));
    }

    [Test]
    public void ConvertTo_UsesConverter_WhenTargetDiffers()
    {
        var weight = new Weight(100, WeightUnits.Kilograms);

        var converted = weight.ConvertTo(WeightUnits.Pounds, (value, _, _) => value * 2.20462);

        Assert.That(converted.Unit, Is.EqualTo(WeightUnits.Pounds));
        Assert.That(converted.Value, Is.EqualTo(220.462).Within(0.001));
    }
}
