using FluentAssertions;
using LgymApi.Application.Features.Measurements;
using LgymApi.Domain.Enums;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class MeasurementUnitResolverTests
{
    [Test]
    public void BodyPartHelpers_ClassifyMeasurementsCorrectly()
    {
        MeasurementUnitResolver.IsSpecialUnitMeasurement(BodyParts.BodyFat).Should().BeTrue();
        MeasurementUnitResolver.IsSpecialUnitMeasurement(BodyParts.Bmi).Should().BeTrue();
        MeasurementUnitResolver.IsWeightMeasurement(BodyParts.BodyWeight).Should().BeTrue();
        MeasurementUnitResolver.IsLengthMeasurement(BodyParts.Waist).Should().BeTrue();
        MeasurementUnitResolver.IsLengthMeasurement(BodyParts.Unknown).Should().BeFalse();
    }

    [Test]
    public void IsUnitAllowedForBodyPart_ValidatesPerMeasurementType()
    {
        MeasurementUnitResolver.IsUnitAllowedForBodyPart(BodyParts.Unknown, MeasurementUnits.Kilograms).Should().BeFalse();
        MeasurementUnitResolver.IsUnitAllowedForBodyPart(BodyParts.BodyWeight, MeasurementUnits.Kilograms).Should().BeTrue();
        MeasurementUnitResolver.IsUnitAllowedForBodyPart(BodyParts.BodyWeight, MeasurementUnits.Centimeters).Should().BeFalse();
        MeasurementUnitResolver.IsUnitAllowedForBodyPart(BodyParts.BodyFat, MeasurementUnits.Percent).Should().BeTrue();
        MeasurementUnitResolver.IsUnitAllowedForBodyPart(BodyParts.Bmi, MeasurementUnits.Bmi).Should().BeTrue();
        MeasurementUnitResolver.IsUnitAllowedForBodyPart(BodyParts.Waist, MeasurementUnits.Centimeters).Should().BeTrue();
    }

    [Test]
    public void GetDefaultUnit_ReturnsExpectedDefaults()
    {
        MeasurementUnitResolver.GetDefaultUnit(BodyParts.BodyWeight).Should().Be(MeasurementUnits.Kilograms);
        MeasurementUnitResolver.GetDefaultUnit(BodyParts.BodyFat).Should().Be(MeasurementUnits.Percent);
        MeasurementUnitResolver.GetDefaultUnit(BodyParts.Bmi).Should().Be(MeasurementUnits.Bmi);
        MeasurementUnitResolver.GetDefaultUnit(BodyParts.Waist).Should().Be(MeasurementUnits.Centimeters);
    }

    [Test]
    public void TryParseStoredUnit_AndUnitConverters_HandleValidAndInvalidValues()
    {
        MeasurementUnitResolver.TryParseStoredUnit("kilograms", out var kilograms).Should().BeTrue();
        kilograms.Should().Be(MeasurementUnits.Kilograms);
        MeasurementUnitResolver.TryParseStoredUnit("unknown", out _).Should().BeFalse();
        MeasurementUnitResolver.TryParseStoredUnit(null, out _).Should().BeFalse();

        MeasurementUnitResolver.TryGetHeightUnit(MeasurementUnits.Meters, out var heightUnit).Should().BeTrue();
        heightUnit.Should().Be(HeightUnits.Meters);
        MeasurementUnitResolver.TryGetHeightUnit(MeasurementUnits.Kilograms, out _).Should().BeFalse();

        MeasurementUnitResolver.TryGetWeightUnit(MeasurementUnits.Pounds, out var weightUnit).Should().BeTrue();
        weightUnit.Should().Be(WeightUnits.Pounds);
        MeasurementUnitResolver.TryGetWeightUnit(MeasurementUnits.Percent, out _).Should().BeFalse();
    }
}
