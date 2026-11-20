using LgymApi.Domain.Measurements;
using LgymApi.Domain.Enums;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class MeasurementUnitResolverTests
{
    [Test]
    public void BodyPartHelpers_ClassifyMeasurementsCorrectly()
    {
        Assert.That(MeasurementUnitResolver.IsSpecialUnitMeasurement(BodyParts.BodyFat), Is.True);
        Assert.That(MeasurementUnitResolver.IsSpecialUnitMeasurement(BodyParts.Bmi), Is.True);
        Assert.That(MeasurementUnitResolver.IsWeightMeasurement(BodyParts.BodyWeight), Is.True);
        Assert.That(MeasurementUnitResolver.IsLengthMeasurement(BodyParts.Waist), Is.True);
        Assert.That(MeasurementUnitResolver.IsLengthMeasurement(BodyParts.Unknown), Is.False);
    }

    [Test]
    public void IsUnitAllowedForBodyPart_ValidatesPerMeasurementType()
    {
        Assert.That(MeasurementUnitResolver.IsUnitAllowedForBodyPart(BodyParts.Unknown, MeasurementUnits.Kilograms), Is.False);
        Assert.That(MeasurementUnitResolver.IsUnitAllowedForBodyPart(BodyParts.BodyWeight, MeasurementUnits.Kilograms), Is.True);
        Assert.That(MeasurementUnitResolver.IsUnitAllowedForBodyPart(BodyParts.BodyWeight, MeasurementUnits.Centimeters), Is.False);
        Assert.That(MeasurementUnitResolver.IsUnitAllowedForBodyPart(BodyParts.BodyFat, MeasurementUnits.Percent), Is.True);
        Assert.That(MeasurementUnitResolver.IsUnitAllowedForBodyPart(BodyParts.Bmi, MeasurementUnits.Bmi), Is.True);
        Assert.That(MeasurementUnitResolver.IsUnitAllowedForBodyPart(BodyParts.Waist, MeasurementUnits.Centimeters), Is.True);
    }

    [Test]
    public void GetDefaultUnit_ReturnsExpectedDefaults()
    {
        Assert.That(MeasurementUnitResolver.GetDefaultUnit(BodyParts.BodyWeight), Is.EqualTo(MeasurementUnits.Kilograms));
        Assert.That(MeasurementUnitResolver.GetDefaultUnit(BodyParts.BodyFat), Is.EqualTo(MeasurementUnits.Percent));
        Assert.That(MeasurementUnitResolver.GetDefaultUnit(BodyParts.Bmi), Is.EqualTo(MeasurementUnits.Bmi));
        Assert.That(MeasurementUnitResolver.GetDefaultUnit(BodyParts.Waist), Is.EqualTo(MeasurementUnits.Centimeters));
    }

    [Test]
    public void TryParseStoredUnit_AndUnitConverters_HandleValidAndInvalidValues()
    {
        Assert.That(MeasurementUnitResolver.TryParseStoredUnit("kilograms", out var kilograms), Is.True);
        Assert.That(kilograms, Is.EqualTo(MeasurementUnits.Kilograms));
        Assert.That(MeasurementUnitResolver.TryParseStoredUnit("unknown", out _), Is.False);
        Assert.That(MeasurementUnitResolver.TryParseStoredUnit(null, out _), Is.False);

        Assert.That(MeasurementUnitResolver.TryGetHeightUnit(MeasurementUnits.Meters, out var heightUnit), Is.True);
        Assert.That(heightUnit, Is.EqualTo(HeightUnits.Meters));
        Assert.That(MeasurementUnitResolver.TryGetHeightUnit(MeasurementUnits.Kilograms, out _), Is.False);

        Assert.That(MeasurementUnitResolver.TryGetWeightUnit(MeasurementUnits.Pounds, out var weightUnit), Is.True);
        Assert.That(weightUnit, Is.EqualTo(WeightUnits.Pounds));
        Assert.That(MeasurementUnitResolver.TryGetWeightUnit(MeasurementUnits.Percent, out _), Is.False);
    }

    [TestCase("kg", MeasurementUnits.Kilograms)]
    [TestCase("lbs", MeasurementUnits.Pounds)]
    [TestCase("cm", MeasurementUnits.Centimeters)]
    [TestCase("mm", MeasurementUnits.Millimeters)]
    [TestCase("%", MeasurementUnits.Percent)]
    public void TryParseStoredUnit_AllowsCommonAliases(string rawUnit, MeasurementUnits expectedUnit)
    {
        Assert.That(MeasurementUnitResolver.TryParseStoredUnit(rawUnit, out var parsedUnit), Is.True);
        Assert.That(parsedUnit, Is.EqualTo(expectedUnit));
    }
}
