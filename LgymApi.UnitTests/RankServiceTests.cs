using FluentAssertions;
using LgymApi.Application.Services;
using LgymApi.Domain.Services;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class RankServiceTests
{
    private readonly RankService _service = new();

    [Test]
    public void GetRanks_ReturnsOrderedDefinitions()
    {
        var ranks = _service.GetRanks();

        ranks.Should().BeSameAs(RankDefinitions.All);
        ranks.Should().HaveCount(10);
        ranks[0].Name.Should().Be("Junior 1");
        ranks[0].NeedElo.Value.Should().Be(0);
        ranks[^1].Name.Should().Be("Champ");
        ranks[^1].NeedElo.Value.Should().Be(30000);
    }

    [TestCase(0, "Junior 1")]
    [TestCase(1000, "Junior 1")]
    [TestCase(1001, "Junior 2")]
    [TestCase(2500, "Junior 3")]
    [TestCase(5999, "Junior 3")]
    [TestCase(6000, "Mid 1")]
    [TestCase(30000, "Champ")]
    [TestCase(35000, "Champ")]
    public void GetCurrentRank_ReturnsExpectedRank(int elo, string expectedName)
    {
        var rank = _service.GetCurrentRank(elo);

        rank.Name.Should().Be(expectedName);
    }

    [Test]
    public void GetNextRank_ReturnsNextRankWhenAvailable()
    {
        var next = _service.GetNextRank("Mid 1");

        next.Should().NotBeNull();
        next!.Name.Should().Be("Mid 2");
    }

    [Test]
    public void GetNextRank_ReturnsNullForLastRank()
    {
        var next = _service.GetNextRank("Champ");

        next.Should().BeNull();
    }

    [Test]
    public void GetNextRank_ReturnsNullForUnknownRank()
    {
        var next = _service.GetNextRank("Unknown");

        next.Should().BeNull();
    }
}
