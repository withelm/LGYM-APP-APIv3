using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class TypedIdValueConverterTests
{
    [Test]
    public void TypedIdValueConverter_ConvertsToGuidAndBack()
    {
        var converter = new TypedIdValueConverter<User>();
        var id = Id<User>.New();

        var toProvider = converter.ConvertToProviderExpression.Compile();
        var fromProvider = converter.ConvertFromProviderExpression.Compile();

        var guid = toProvider(id);
        var roundtrip = fromProvider(guid);

        roundtrip.Should().Be(id);
    }

    [Test]
    public void NullableTypedIdValueConverter_HandlesNullAndValue()
    {
        var converter = new NullableTypedIdValueConverter<User>();
        var id = Id<User>.New();

        var toProvider = converter.ConvertToProviderExpression.Compile();
        var fromProvider = converter.ConvertFromProviderExpression.Compile();

        var guid = toProvider(id);
        var nullGuid = toProvider(null);
        var roundtrip = fromProvider(guid);
        var nullRoundtrip = fromProvider(null);

        guid.Should().Be(id.GetValue());
        nullGuid.Should().BeNull();
        roundtrip.Should().Be(id);
        nullRoundtrip.Should().BeNull();
    }

    [Test]
    public void NullableIdValueComparer_CoversNullAndValueComparisons()
    {
        var comparer = new NullableIdValueComparer<User>();
        var left = Id<User>.New();
        var right = Id<User>.New();

        comparer.Equals(null, null).Should().BeTrue();
        comparer.Equals(left, null).Should().BeFalse();
        comparer.Equals(null, right).Should().BeFalse();
        comparer.Equals(left, left).Should().BeTrue();
        comparer.Equals(left, right).Should().BeFalse();
        comparer.GetHashCode(null).Should().Be(0);
        comparer.GetHashCode(left).Should().NotBe(0);
    }
}
