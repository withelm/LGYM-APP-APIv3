using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;

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

        Assert.That(roundtrip, Is.EqualTo(id));
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

        Assert.Multiple(() =>
        {
            Assert.That(guid, Is.EqualTo(id.GetValue()));
            Assert.That(nullGuid, Is.Null);
            Assert.That(roundtrip, Is.EqualTo(id));
            Assert.That(nullRoundtrip, Is.Null);
        });
    }

    [Test]
    public void NullableIdValueComparer_CoversNullAndValueComparisons()
    {
        var comparer = new NullableIdValueComparer<User>();
        var left = Id<User>.New();
        var right = Id<User>.New();

        Assert.Multiple(() =>
        {
            Assert.That(comparer.Equals(null, null), Is.True);
            Assert.That(comparer.Equals(left, null), Is.False);
            Assert.That(comparer.Equals(null, right), Is.False);
            Assert.That(comparer.Equals(left, left), Is.True);
            Assert.That(comparer.Equals(left, right), Is.False);
            Assert.That(comparer.GetHashCode(null), Is.EqualTo(0));
            Assert.That(comparer.GetHashCode(left), Is.Not.EqualTo(0));
        });
    }
}
