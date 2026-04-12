using FluentAssertions;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class WeightValueObjectTests
{
    [Test]
    public void ConvertTo_ReturnsSame_WhenTargetMatches()
    {
        var weight = new Weight(100, WeightUnits.Kilograms);

        var converted = weight.ConvertTo(WeightUnits.Kilograms, (_, _, _) => throw new InvalidOperationException());

        converted.Should().Be(weight);
    }

    [Test]
    public void ConvertTo_UsesConverter_WhenTargetDiffers()
    {
        var weight = new Weight(100, WeightUnits.Kilograms);

        var converted = weight.ConvertTo(WeightUnits.Pounds, (value, _, _) => value * 2.20462);

        converted.Unit.Should().Be(WeightUnits.Pounds);
        converted.Value.Should().BeApproximately(220.462, 0.001);
    }
}
