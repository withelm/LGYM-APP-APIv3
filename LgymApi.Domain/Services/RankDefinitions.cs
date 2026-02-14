namespace LgymApi.Domain.Services;

public sealed class RankDefinition
{
    public string Name { get; init; } = string.Empty;
    public int NeedElo { get; init; }
}

public static class RankDefinitions
{
    public static IReadOnlyList<RankDefinition> All { get; } = new List<RankDefinition>
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
}
