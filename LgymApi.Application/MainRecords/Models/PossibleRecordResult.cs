using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.MainRecords.Models;

public sealed class PossibleRecordResult
{
    public double Weight { get; init; }
    public int Reps { get; init; }
    public WeightUnits Unit { get; init; }
    public DateTime Date { get; init; }
}
