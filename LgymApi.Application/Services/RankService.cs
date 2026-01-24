namespace LgymApi.Application.Services;

public sealed class RankDefinition
{
    public string Name { get; init; } = string.Empty;
    public int NeedElo { get; init; }
}

public interface IRankService
{
    IReadOnlyList<RankDefinition> GetRanks();
    RankDefinition GetCurrentRank(int elo);
    RankDefinition? GetNextRank(string currentRankName);
}

public sealed class RankService : IRankService
{
    private static readonly List<RankDefinition> Ranks = new()
    {
        new RankDefinition { Name = "Junior 1", NeedElo = 0 },
        new RankDefinition { Name = "Junior 2", NeedElo = 1001 },
        new RankDefinition { Name = "Junior 3", NeedElo = 2500 },
        new RankDefinition { Name = "Mid 1", NeedElo = 6000 },
        new RankDefinition { Name = "Mid 2", NeedElo = 8000 },
        new RankDefinition { Name = "Mid 3", NeedElo = 12000 },
        new RankDefinition { Name = "Pro 1", NeedElo = 15000 },
        new RankDefinition { Name = "Pro 2", NeedElo = 20000 },
        new RankDefinition { Name = "Pro 3", NeedElo = 24000 },
        new RankDefinition { Name = "Champ", NeedElo = 30000 }
    };

    public IReadOnlyList<RankDefinition> GetRanks() => Ranks;

    public RankDefinition GetCurrentRank(int elo)
    {
        return Ranks.Last(rank => elo >= rank.NeedElo);
    }

    public RankDefinition? GetNextRank(string currentRankName)
    {
        for (var i = 0; i < Ranks.Count; i++)
        {
            if (Ranks[i].Name == currentRankName)
            {
                return i + 1 < Ranks.Count ? Ranks[i + 1] : null;
            }
        }

        return null;
    }
}
