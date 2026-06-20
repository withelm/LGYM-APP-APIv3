using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Trainer.Controllers;

[ApiController]
[Route("api/trainer")]
[Authorize(Policy = AuthConstants.Policies.TrainerAccess)]
public sealed class TrainerReportingController : ControllerBase
{
    private readonly IReportingService _reportingService;
    private readonly IMapper _mapper;

    public TrainerReportingController(IReportingService reportingService, IMapper mapper)
    {
        _reportingService = reportingService;
        _mapper = mapper;
    }

    [HttpPost("report-templates")]
    [ProducesResponseType(typeof(ReportTemplateDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTemplate([FromBody] UpsertReportTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var trainer = HttpContext.GetCurrentUser();
        var result = await _reportingService.CreateTemplateAsync(trainer!, new CreateReportTemplateCommand
        {
            Name = request.Name,
            Description = request.Description,
            Fields = request.Fields.Select(MapField).ToList()
        }, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return StatusCode(StatusCodes.Status201Created, _mapper.Map<ReportTemplateResult, ReportTemplateDto>(result.Value));
    }

    [HttpGet("report-templates")]
    [ProducesResponseType(typeof(List<ReportTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTemplates(CancellationToken cancellationToken = default)
    {
        var trainer = HttpContext.GetCurrentUser();
        var result = await _reportingService.GetTrainerTemplatesAsync(trainer!, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.MapList<ReportTemplateResult, ReportTemplateDto>(result.Value));
    }

    [HttpGet("report-templates/{templateId}")]
    [ProducesResponseType(typeof(ReportTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTemplate([FromRoute] string templateId, CancellationToken cancellationToken = default)
    {
        if (!Id<ReportTemplate>.TryParse(templateId, out var parsedTemplateId))
        {
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.FieldRequired));
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _reportingService.GetTrainerTemplateAsync(trainer!, parsedTemplateId, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<ReportTemplateResult, ReportTemplateDto>(result.Value));
    }

    [HttpPost("report-templates/{templateId}/update")]
    [ProducesResponseType(typeof(ReportTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateTemplate([FromRoute] string templateId, [FromBody] UpsertReportTemplateRequest request, CancellationToken cancellationToken = default)
    {
        if (!Id<ReportTemplate>.TryParse(templateId, out var parsedTemplateId))
        {
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.FieldRequired));
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _reportingService.UpdateTemplateAsync(trainer!, parsedTemplateId, new CreateReportTemplateCommand
        {
            Name = request.Name,
            Description = request.Description,
            Fields = request.Fields.Select(MapField).ToList()
        }, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<ReportTemplateResult, ReportTemplateDto>(result.Value));
    }

    [HttpPost("report-templates/{templateId}/delete")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteTemplate([FromRoute] string templateId, CancellationToken cancellationToken = default)
    {
        if (!Id<ReportTemplate>.TryParse(templateId, out var parsedTemplateId))
        {
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.FieldRequired));
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _reportingService.DeleteTemplateAsync(trainer!, parsedTemplateId, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Deleted));
    }

    [HttpPost("trainees/{traineeId}/report-requests")]
    [ProducesResponseType(typeof(ReportRequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateReportRequest([FromRoute] string traineeId, [FromBody] CreateReportRequestRequest request, CancellationToken cancellationToken = default)
    {
        if (!Id<LgymApi.Domain.Entities.User>.TryParse(traineeId, out var parsedTraineeId))
        {
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.UserIdRequired));
        }

        if (!Id<ReportTemplate>.TryParse(request.TemplateId, out var parsedTemplateId))
        {
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.FieldRequired));
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _reportingService.CreateReportRequestAsync(trainer!, parsedTraineeId, new CreateReportRequestCommand
        {
            TemplateId = parsedTemplateId,
            DueAt = request.DueAt,
            Note = request.Note
        }, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return StatusCode(StatusCodes.Status201Created, _mapper.Map<ReportRequestResult, ReportRequestDto>(result.Value));
    }

    [HttpGet("trainees/{traineeId}/report-submissions")]
    [ProducesResponseType(typeof(List<ReportSubmissionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTraineeSubmissions([FromRoute] string traineeId, CancellationToken cancellationToken = default)
    {
        if (!Id<LgymApi.Domain.Entities.User>.TryParse(traineeId, out var parsedTraineeId))
        {
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.UserIdRequired));
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _reportingService.GetTraineeSubmissionsAsync(trainer!, parsedTraineeId, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.MapList<ReportSubmissionResult, ReportSubmissionDto>(result.Value));
    }

    [HttpPost("trainees/{traineeId}/report-submissions/{submissionId}/feedback")]
    [ProducesResponseType(typeof(ReportSubmissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSubmissionFeedback([FromRoute] string traineeId, [FromRoute] string submissionId, [FromBody] UpdateReportSubmissionFeedbackRequest request, CancellationToken cancellationToken = default)
    {
        if (!Id<LgymApi.Domain.Entities.User>.TryParse(traineeId, out var parsedTraineeId))
        {
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.UserIdRequired));
        }

        if (!Id<ReportSubmission>.TryParse(submissionId, out var parsedSubmissionId))
        {
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.FieldRequired));
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _reportingService.UpdateTrainerFeedbackAsync(
            trainer!,
            parsedTraineeId,
            parsedSubmissionId,
            new UpdateReportSubmissionFeedbackCommand
            {
                TrainerOverallComment = request.TrainerOverallComment,
                FieldComments = new Dictionary<string, string?>(request.TrainerFieldComments, StringComparer.OrdinalIgnoreCase)
            },
            cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<ReportSubmissionResult, ReportSubmissionDto>(result.Value));
    }

    [HttpPost("reporting/photos/upload-init")]
    [ProducesResponseType(typeof(InitiatePhotoUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InitiatePhotoUpload([FromBody] InitiatePhotoUploadRequest request, CancellationToken cancellationToken = default)
    {
        if (!Id<ReportRequest>.TryParse(request.ReportRequestId, out var parsedRequestId))
        {
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.FieldRequired));
        }

        var currentUser = HttpContext.GetCurrentUser();
        var result = await _reportingService.InitiatePhotoUploadAsync(
            currentUser!,
            new InitiatePhotoUploadCommand
            {
                ReportRequestId = parsedRequestId,
                ViewType = request.ViewType,
                MimeType = request.MimeType,
                SizeBytes = request.SizeBytes
            },
            cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<InitiatePhotoUploadResult, InitiatePhotoUploadResponse>(result.Value));
    }

    [HttpGet("reporting/photos/{photoId}/signed-url")]
    [ProducesResponseType(typeof(GetSignedReadUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPhotoSignedReadUrl([FromRoute] string photoId, CancellationToken cancellationToken = default)
    {
        var currentUser = HttpContext.GetCurrentUser();
        var result = await _reportingService.GetSignedReadUrlAsync(currentUser!, photoId, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<SignedReadUrlResult, GetSignedReadUrlResponse>(result.Value));
    }

    [HttpPost("reporting/photos/complete-upload")]
    [ProducesResponseType(typeof(CompletePhotoUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompletePhotoUpload([FromBody] CompletePhotoUploadRequest request, CancellationToken cancellationToken = default)
    {
        if (!Id<ReportRequest>.TryParse(request.ReportRequestId, out var parsedRequestId))
        {
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.FieldRequired));
        }

        var currentUser = HttpContext.GetCurrentUser();
        var result = await _reportingService.CompletePhotoUploadAsync(
            currentUser!,
            new CompletePhotoUploadCommand
            {
                StorageKey = request.StorageKey,
                MimeType = request.MimeType,
                SizeBytes = request.SizeBytes,
                Checksum = request.Checksum,
                ReportRequestId = parsedRequestId,
                ViewType = request.ViewType
            },
            cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<CompletePhotoUploadResult, CompletePhotoUploadResponse>(result.Value));
    }

    [HttpGet("reporting/photos/history")]
    [ProducesResponseType(typeof(GetPhotoHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPhotoHistory([FromQuery] string? traineeId, [FromQuery] string? requestId, CancellationToken cancellationToken = default)
    {
        Id<LgymApi.Domain.Entities.User>? parsedTraineeId = null;
        if (!string.IsNullOrWhiteSpace(traineeId))
        {
            if (!Id<LgymApi.Domain.Entities.User>.TryParse(traineeId, out var tempId))
            {
                return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.FieldRequired));
            }
            parsedTraineeId = tempId;
        }

        Id<ReportRequest>? parsedRequestId = null;
        if (!string.IsNullOrWhiteSpace(requestId))
        {
            if (!Id<ReportRequest>.TryParse(requestId, out var tempId))
            {
                return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.FieldRequired));
            }
            parsedRequestId = tempId;
        }

        var currentUser = HttpContext.GetCurrentUser();
        var result = await _reportingService.GetPhotoHistoryAsync(
            currentUser!,
            new GetPhotoHistoryCommand
            {
                TraineeId = parsedTraineeId,
                RequestId = parsedRequestId
            },
            cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        var response = new GetPhotoHistoryResponse
        {
            Photos = _mapper.MapList<PhotoHistoryItemResult, PhotoHistoryItemResponse>(result.Value)
        };

        return Ok(response);
    }

    private static ReportTemplateFieldCommand MapField(ReportTemplateFieldRequest field)
    {
        return new ReportTemplateFieldCommand
        {
            Key = field.Key,
            Label = field.Label,
            Type = field.Type,
            IsRequired = field.IsRequired,
            Order = field.Order,
            ModuleConfig = field.ModuleConfig
        };
    }
}
