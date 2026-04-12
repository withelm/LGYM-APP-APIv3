using FluentAssertions;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Features.Enum;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class EnumServiceTests
{
    [Test]
    public async Task GetLookupByNameAsync_WithInvalidEnumTypeName_ReturnsInvalidEnumError()
    {
        var service = new EnumService();

        var result = await service.GetLookupByNameAsync("NonExistentEnum");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidEnumError>();
    }
}
