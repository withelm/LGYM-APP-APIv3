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

        Assert.That(ranks, Is.SameAs(RankDefinitions.All));
        Assert.That(ranks, Has.Count.EqualTo(10));
        Assert.That(ranks[0].Name, Is.EqualTo("Junior 1"));
        Assert.That(ranks[0].NeedElo, Is.EqualTo(0));
        Assert.That(ranks[^1].Name, Is.EqualTo("Champ"));
        Assert.That(ranks[^1].NeedElo, Is.EqualTo(30000));
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

        Assert.That(rank.Name, Is.EqualTo(expectedName));
    }

    [Test]
    public void GetNextRank_ReturnsNextRankWhenAvailable()
    {
        var next = _service.GetNextRank("Mid 1");

        Assert.That(next, Is.Not.Null);
        Assert.That(next!.Name, Is.EqualTo("Mid 2"));
    }

    [Test]
    public void GetNextRank_ReturnsNullForLastRank()
    {
        var next = _service.GetNextRank("Champ");

        Assert.That(next, Is.Null);
    }

    [Test]
    public void GetNextRank_ReturnsNullForUnknownRank()
    {
        var next = _service.GetNextRank("Unknown");

        Assert.That(next, Is.Null);
    }
}
