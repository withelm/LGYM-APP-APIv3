using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.Measurements.Models;

public sealed class MeasurementCreateInput
{
    public BodyParts BodyPart { get; init; }
    public MeasurementUnits Unit { get; init; }
    public double Value { get; init; }
}
