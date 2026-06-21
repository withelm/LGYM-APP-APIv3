using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using LgymApi.Application.Features.Reporting;
using LgymApi.Domain.Entities;
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

        var result = InvokeBool("TryReadRequiredPhotoViews", config, out var views);

        result.Should().BeTrue();
        views.Should().BeEquivalentTo(new[] { PhotoViewType.Front, PhotoViewType.Side, PhotoViewType.Back });
    }

    [Test]
    public void TryReadRequiredPhotoViews_WithInvalidElementKind_ReturnsFalse()
    {
        var config = JsonDocument.Parse("""
            { "requiredViews": [1] }
            """).RootElement;

        var result = InvokeBool("TryReadRequiredPhotoViews", config, out _);

        result.Should().BeFalse();
    }

    [Test]
    public void TryReadMeasurementTypes_WithAliases_ReturnsResolvedBodyParts()
    {
        var config = JsonDocument.Parse("""
            { "measurementTypes": ["weight", "bodyFat", "thighs"] }
            """).RootElement;

        var result = InvokeBool("TryReadMeasurementTypes", config, out var types);

        result.Should().BeTrue();
        types.Should().BeEquivalentTo(new[] { BodyParts.BodyWeight, BodyParts.BodyFat, BodyParts.Thigh });
    }

    [Test]
    public void TryReadMeasurementTypes_WithUnknownOrUnknownEnum_ReturnsFalse()
    {
        var config = JsonDocument.Parse("""
            { "measurementTypes": ["unknown"] }
            """).RootElement;

        var result = InvokeBool("TryReadMeasurementTypes", config, out _);

        result.Should().BeFalse();
    }

    [Test]
    public void TryGetArrayProperty_WhenPropertyMissingOrWrongKind_ReturnsFalse()
    {
        var config = JsonDocument.Parse("""
            { "requiredViews": "front" }
            """).RootElement;
        var method = typeof(ReportingService).GetMethod("TryGetArrayProperty", BindingFlags.Static | BindingFlags.NonPublic)!;
        var args = new object?[] { config, "missing", null };
        var missing = (bool)method.Invoke(null, args)!;
        args = new object?[] { config, "requiredViews", null };
        var wrongKind = (bool)method.Invoke(null, args)!;

        missing.Should().BeFalse();
        wrongKind.Should().BeFalse();
    }

    private static bool InvokeBool(string methodName, JsonElement config, out HashSet<Enum> values)
    {
        var method = typeof(ReportingService).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!;
        var args = new object?[] { config, null };
        var result = (bool)method.Invoke(null, args)!;
        values = ((System.Collections.IEnumerable)args[1]!).Cast<Enum>().ToHashSet();
        return result;
    }
}
