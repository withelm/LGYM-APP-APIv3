using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Mapping.Core;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class ReportingProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<ReportTemplateFieldResult, ReportTemplateFieldDto>((source, _) => new ReportTemplateFieldDto
        {
            Key = source.Key,
            Label = source.Label,
            Type = source.Type,
            IsRequired = source.IsRequired,
            Order = source.Order
        });

        configuration.CreateMap<ReportTemplateResult, ReportTemplateDto>((source, mapper) => new ReportTemplateDto
        {
            Id = source?.Id.ToString() ?? string.Empty,
            TrainerId = source?.TrainerId.ToString() ?? string.Empty,
            Name = source?.Name ?? string.Empty,
            Description = source?.Description,
            CreatedAt = source?.CreatedAt ?? default,
            Fields = MapTemplateFields(source, mapper)
        });

        configuration.CreateMap<ReportRequestResult, ReportRequestDto>((source, mapper) => new ReportRequestDto
        {
            Id = source?.Id.ToString() ?? string.Empty,
            TrainerId = source?.TrainerId.ToString() ?? string.Empty,
            TraineeId = source?.TraineeId.ToString() ?? string.Empty,
            TemplateId = source?.TemplateId.ToString() ?? string.Empty,
            Status = source?.Status ?? default,
            DueAt = source?.DueAt,
            Note = source?.Note,
            CreatedAt = source?.CreatedAt ?? default,
            SubmittedAt = source?.SubmittedAt,
            Template = MapTemplate(source?.Template, mapper)
        });

        configuration.CreateMap<ReportSubmissionResult, ReportSubmissionDto>((source, mapper) => new ReportSubmissionDto
        {
            Id = source?.Id.ToString() ?? string.Empty,
            ReportRequestId = source?.ReportRequestId.ToString() ?? string.Empty,
            TraineeId = source?.TraineeId.ToString() ?? string.Empty,
            SubmittedAt = source?.SubmittedAt ?? default,
            Answers = source?.Answers ?? new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.OrdinalIgnoreCase),
            Request = MapRequest(source?.Request, mapper)
        });
    }

    private static List<ReportTemplateFieldDto> MapTemplateFields(ReportTemplateResult? source, MappingContext? mapper)
    {
        if (source?.Fields == null || source.Fields.Count == 0 || mapper == null)
        {
            return [];
        }

        return mapper.MapList<ReportTemplateFieldResult, ReportTemplateFieldDto>(source.Fields);
    }

    private static ReportTemplateDto MapTemplate(ReportTemplateResult? source, MappingContext? mapper)
    {
        if (source == null || mapper == null)
        {
            return new ReportTemplateDto();
        }

        return mapper.Map<ReportTemplateResult, ReportTemplateDto>(source) ?? new ReportTemplateDto();
    }

    private static ReportRequestDto MapRequest(ReportRequestResult? source, MappingContext? mapper)
    {
        if (source == null || mapper == null)
        {
            return new ReportRequestDto();
        }

        return mapper.Map<ReportRequestResult, ReportRequestDto>(source) ?? new ReportRequestDto();
    }
}
