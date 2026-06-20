using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.Measurements.Models;

public sealed class MeasurementTrendResult
{
    public BodyParts BodyPart { get; set; }
    public MeasurementUnits Unit { get; set; }
    public double? StartValue { get; set; }
    public double? CurrentValue { get; set; }
    public double? Change { get; set; }
    public double? ChangePercentage { get; set; }
    public double? FirstMeasurementValue { get; set; }
    public DateTimeOffset? FirstMeasurementDate { get; set; }
    public double? LastMeasurementValue { get; set; }
    public DateTimeOffset? LastMeasurementDate { get; set; }
    public double? Difference { get; set; }
    public string Direction { get; set; } = string.Empty;
    public int Points { get; set; }
}
