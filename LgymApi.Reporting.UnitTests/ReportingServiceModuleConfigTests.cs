using System.Text.Json;
using LgymApi.Application.Features.Reporting;
using LgymApi.Domain.Enums;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ReportingServiceModuleConfigTests
{
    [Test]
    public void TryReadRequiredPhotoViews_WithValidValues_ReturnsParsedViews()
    {
        var config = JsonDocument.Parse("""
            { "requiredViews": ["front", "Side", "back"] }
            """).RootElement;

        var result = ReportingModuleConfigParser.TryNormalizePhotoModuleConfig(config, out _, out var views);

        Assert.That(result, Is.True);
        Assert.That(views, Is.EquivalentTo(new[] { "Front", "Side", "Back" }));
    }

    [Test]
    public void TryReadRequiredPhotoViews_WithLeftAndRightSides_ReturnsParsedViews()
    {
        var config = JsonDocument.Parse("""
            { "requiredViews": ["front", "SideLeft", "SideRight", "back"] }
            """).RootElement;

        var result = ReportingModuleConfigParser.TryNormalizePhotoModuleConfig(config, out _, out var views);

        Assert.That(result, Is.True);
        Assert.That(views, Is.EquivalentTo(new[] { "Front", "SideLeft", "SideRight", "Back" }));
    }

    [Test]
    public void TryReadRequiredPhotoViews_WithInvalidElementKind_ReturnsFalse()
    {
        var config = JsonDocument.Parse("""
            { "requiredViews": [1] }
            """).RootElement;

        var result = ReportingModuleConfigParser.TryNormalizePhotoModuleConfig(config, out _, out _);

        Assert.That(result, Is.False);
    }

    [Test]
    public void TryReadMeasurementTypes_WithAliases_ReturnsResolvedBodyParts()
    {
        var config = JsonDocument.Parse("""
            { "measurementTypes": ["weight", "bodyFat", "thighs"] }
            """).RootElement;

        var result = ReportingModuleConfigParser.TryNormalizeMeasurementModuleConfig(config, out _, out var types);

        Assert.That(result, Is.True);
        Assert.That(types, Is.EquivalentTo(new[] { BodyParts.BodyWeight, BodyParts.BodyFat, BodyParts.Thigh }));
    }

    [Test]
    public void TryReadMeasurementTypes_WithUnknownOrUnknownEnum_ReturnsFalse()
    {
        var config = JsonDocument.Parse("""
            { "measurementTypes": ["unknown"] }
            """).RootElement;

        var result = ReportingModuleConfigParser.TryNormalizeMeasurementModuleConfig(config, out _, out _);

        Assert.That(result, Is.False);
    }

    [Test]
    public void TryNormalizePhotoModuleConfig_WhenPropertyMissingOrWrongKind_ReturnsFalse()
    {
        var missingConfig = JsonDocument.Parse("""
            { "wrongProperty": ["front"] }
            """).RootElement;
        var wrongKindConfig = JsonDocument.Parse("""
            { "requiredViews": "front" }
            """).RootElement;
        var missing = ReportingModuleConfigParser.TryNormalizePhotoModuleConfig(missingConfig, out _, out _);
        var wrongKind = ReportingModuleConfigParser.TryNormalizePhotoModuleConfig(wrongKindConfig, out _, out _);

        Assert.That(missing, Is.False);
        Assert.That(wrongKind, Is.False);
    }
}
