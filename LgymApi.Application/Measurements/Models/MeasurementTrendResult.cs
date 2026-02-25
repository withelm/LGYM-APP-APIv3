using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.Measurements.Models;

public sealed class MeasurementTrendResult
{
    public BodyParts BodyPart { get; set; }
    public HeightUnits Unit { get; set; }
    public double StartValue { get; set; }
    public double CurrentValue { get; set; }
    public double Change { get; set; }
    public double ChangePercentage { get; set; }
    public string Direction { get; set; } = string.Empty;
    public int Points { get; set; }
}
