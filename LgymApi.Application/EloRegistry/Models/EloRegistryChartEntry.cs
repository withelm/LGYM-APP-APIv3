namespace LgymApi.Application.Features.EloRegistry.Models;

public sealed class EloRegistryChartEntry
{
    public string Id { get; init; } = string.Empty;
    public int Value { get; init; }
    public string Date { get; init; } = string.Empty;
}
