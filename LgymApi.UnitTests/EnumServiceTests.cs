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

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.TypeOf<InvalidEnumError>());
        });
    }
}
