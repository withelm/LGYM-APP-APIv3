using LgymApi.Api.Features.Enum;
using LgymApi.Api.Features.Measurements.Contracts;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class MeasurementProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<Measurement, MeasurementResponseDto>((source, _) => new MeasurementResponseDto
        {
            UserId = source.UserId.ToString(),
            BodyPart = source.BodyPart.ToLookup(),
            Unit = ParseUnit(source.Unit).ToLookup(),
            Value = source.Value,
            CreatedAt = source.CreatedAt.UtcDateTime,
            UpdatedAt = source.UpdatedAt.UtcDateTime
        });

        configuration.CreateMap<List<Measurement>, MeasurementsHistoryDto>((source, context) => new MeasurementsHistoryDto
        {
            Measurements = context!.MapList<Measurement, MeasurementResponseDto>(source)
        });
    }

    private static HeightUnits ParseUnit(string? unit)
    {
        if (!string.IsNullOrWhiteSpace(unit) && Enum.TryParse(unit, true, out HeightUnits parsed))
        {
            return parsed;
        }

        return HeightUnits.Unknown;
    }
}
