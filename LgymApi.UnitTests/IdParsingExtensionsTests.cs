using FluentAssertions;
using LgymApi.Api.Middleware;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class IdParsingExtensionsTests
{
    [Test]
    public void ToIdOrEmpty_ReturnsParsedId_ForValidInput()
    {
        var expected = Id<User>.New();

        var actual = expected.ToString().ToIdOrEmpty<User>();

        actual.Should().Be(expected);
    }

    [Test]
    public void ToIdOrEmpty_ReturnsEmpty_ForNullOrInvalidInput()
    {
        var fromNull = ((string?)null).ToIdOrEmpty<User>();
        var fromInvalid = "not-a-guid".ToIdOrEmpty<User>();

        fromNull.IsEmpty.Should().BeTrue();
        fromInvalid.IsEmpty.Should().BeTrue();
    }

    [Test]
    public void ToNullableId_ReturnsParsedId_ForValidInput()
    {
        var expected = Id<Exercise>.New();

        var actual = expected.ToString().ToNullableId<Exercise>();

        actual.Should().Be(expected);
    }

    [Test]
    public void ToNullableId_ReturnsNull_ForNullOrInvalidInput()
    {
        var fromNull = ((string?)null).ToNullableId<Exercise>();
        var fromInvalid = "invalid".ToNullableId<Exercise>();

        fromNull.Should().BeNull();
        fromInvalid.Should().BeNull();
    }
}
