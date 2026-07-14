using System.Globalization;
using FluentAssertions;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Features.Enum;
using LgymApi.Domain.Enums;

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

    [Test]
    public void GetLookup_Should_Return_Enum_Name_Id_And_Translated_Labels()
    {
        var service = new EnumService();

        var values = service.GetLookup<ExerciseEloFormula>(CultureInfo.GetCultureInfo("en"));

        var pullupWeighted = values.Single(v => v.Id == ExerciseEloFormula.PullupWeighted.ToString());
        pullupWeighted.Name.Should().Be(ExerciseEloFormula.PullupWeighted.ToString());
        pullupWeighted.DisplayName.Should().Be("Pull-up weighted");
    }
}
