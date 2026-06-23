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
            Order = source.Order,
            ModuleConfig = source.ModuleConfig
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
            TrainerOverallComment = source?.TrainerOverallComment,
            TrainerFieldComments = source?.TrainerFieldComments ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            TrainerFeedbackAddedAt = source?.TrainerFeedbackAddedAt,
            TrainerFeedbackReadAt = source?.TrainerFeedbackReadAt,
            Request = MapRequest(source?.Request, mapper)
        });

        configuration.CreateMap<RecurringReportAssignmentResult, RecurringReportAssignmentDto>((source, mapper) => new RecurringReportAssignmentDto
        {
            Id = source?.Id.ToString() ?? string.Empty,
            TrainerId = source?.TrainerId.ToString() ?? string.Empty,
            TraineeId = source?.TraineeId.ToString() ?? string.Empty,
            TemplateId = source?.TemplateId.ToString() ?? string.Empty,
            IntervalValue = source?.IntervalValue ?? 0,
            IntervalUnit = source?.IntervalUnit ?? default,
            StartsAt = source?.StartsAt ?? default,
            EndsAt = source?.EndsAt,
            IsActive = source?.IsActive ?? false,
            Note = source?.Note,
            CurrentReportRequestId = source?.CurrentReportRequestId?.ToString(),
            LastRequestCreatedAt = source?.LastRequestCreatedAt,
            NextEligibleAt = source?.NextEligibleAt,
            CreatedAt = source?.CreatedAt ?? default,
            Template = MapTemplate(source?.Template, mapper),
            CurrentReportRequest = MapNullableRequest(source?.CurrentReportRequest, mapper)
        });

        configuration.CreateMap<InitiatePhotoUploadResult, InitiatePhotoUploadResponse>((source, _) => new InitiatePhotoUploadResponse
        {
            UploadUrl = source?.UploadUrl ?? string.Empty,
            StorageKey = source?.StorageKey ?? string.Empty,
            ExpiresAt = source?.ExpiresAt ?? default
        });

        configuration.CreateMap<SignedReadUrlResult, GetSignedReadUrlResponse>((source, _) => new GetSignedReadUrlResponse
        {
            ReadUrl = source?.ReadUrl ?? string.Empty,
            ExpiresAt = source?.ExpiresAt ?? default
        });

        configuration.CreateMap<CompletePhotoUploadResult, CompletePhotoUploadResponse>((source, _) => new CompletePhotoUploadResponse
        {
            PhotoId = source?.PhotoId.ToString() ?? string.Empty,
            UploadedAt = source?.UploadedAt ?? default
        });

        configuration.CreateMap<PhotoHistoryItemResult, PhotoHistoryItemResponse>((source, _) => new PhotoHistoryItemResponse
        {
            Id = source?.Id.ToString() ?? string.Empty,
            ViewType = source?.ViewType ?? string.Empty,
            SizeBytes = source?.SizeBytes ?? 0,
            ThumbnailUrl = source?.ThumbnailUrl,
            ReadUrl = source?.ReadUrl ?? string.Empty,
            ReportRequestId = source?.ReportRequestId.ToString() ?? string.Empty,
            UploadedAt = source?.UploadedAt ?? default
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

    private static ReportRequestDto? MapNullableRequest(ReportRequestResult? source, MappingContext? mapper)
    {
        if (source == null || mapper == null)
        {
            return null;
        }

        return mapper.Map<ReportRequestResult, ReportRequestDto>(source);
    }
}
