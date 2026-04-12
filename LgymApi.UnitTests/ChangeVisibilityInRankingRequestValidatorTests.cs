using FluentAssertions;
using LgymApi.Api.Features.User.Contracts;
using LgymApi.Api.Features.User.Validation;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ChangeVisibilityInRankingRequestValidatorTests
{
    [Test]
    public void Validate_Fails_WhenIsVisibleInRankingIsNull()
    {
        var validator = new ChangeVisibilityInRankingRequestValidator();
        var request = new ChangeVisibilityInRankingRequest { IsVisibleInRanking = null };

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_Passes_WhenIsVisibleInRankingIsTrue()
    {
        var validator = new ChangeVisibilityInRankingRequestValidator();
        var request = new ChangeVisibilityInRankingRequest { IsVisibleInRanking = true };

        var result = validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_Passes_WhenIsVisibleInRankingIsFalse()
    {
        var validator = new ChangeVisibilityInRankingRequestValidator();
        var request = new ChangeVisibilityInRankingRequest { IsVisibleInRanking = false };

        var result = validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }
}
