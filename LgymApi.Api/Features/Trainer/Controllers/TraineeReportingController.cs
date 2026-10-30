using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Reporting.ApiAdapters;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Trainer.Controllers;

[ApiController]
[Route("api/trainee")]
[Authorize]
[ReportingProtocol]
public sealed class TraineeReportingController : ControllerBase
{
    private readonly ITraineeReportRequestApiPort _requests;
    private readonly ITraineeReportPhotoApiPort _photos;
    private readonly IMapper _mapper;

    public TraineeReportingController(ITraineeReportRequestApiPort requests, ITraineeReportPhotoApiPort photos, IMapper mapper)
    {
        _requests = requests;
        _photos = photos;
        _mapper = mapper;
    }

    [HttpGet("report-requests")]
    [ProducesResponseType(typeof(List<ReportRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingRequests(CancellationToken cancellationToken = default)
    {
        var result = await _requests.GetPendingAsync(HttpContext.GetAuthenticatedAccountContext()!, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.MapList<ReportRequestResult, ReportRequestDto>(result.Value));
    }

    [HttpPost("report-requests/{requestId}/submit")]
    [ProducesResponseType(typeof(ReportSubmissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitRequest([FromRoute] string requestId, [FromBody] SubmitReportRequestRequest request, CancellationToken cancellationToken = default)
    {
        if (!Id<ReportRequest>.TryParse(requestId, out var parsedRequestId))
        {
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.FieldRequired));
        }

        var result = await _requests.SubmitAsync(HttpContext.GetAuthenticatedAccountContext()!, parsedRequestId, new SubmitReportRequestCommand
        {
            Answers = new Dictionary<string, System.Text.Json.JsonElement>(request.Answers, StringComparer.OrdinalIgnoreCase)
        }, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<ReportSubmissionResult, ReportSubmissionDto>(result.Value));
    }

    [HttpGet("report-submissions")]
    [ProducesResponseType(typeof(List<ReportSubmissionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOwnSubmissions(CancellationToken cancellationToken = default)
    {
        var result = await _requests.GetOwnSubmissionsAsync(HttpContext.GetAuthenticatedAccountContext()!, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.MapList<ReportSubmissionResult, ReportSubmissionDto>(result.Value));
    }

    [HttpPost("report-submissions/{submissionId}/mark-feedback-read")]
    [ProducesResponseType(typeof(ReportSubmissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MarkFeedbackRead([FromRoute] string submissionId, CancellationToken cancellationToken = default)
    {
        if (!Id<ReportSubmission>.TryParse(submissionId, out var parsedSubmissionId))
        {
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.FieldRequired));
        }

        var result = await _requests.MarkFeedbackReadAsync(HttpContext.GetAuthenticatedAccountContext()!, parsedSubmissionId, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<ReportSubmissionResult, ReportSubmissionDto>(result.Value));
    }

    [HttpPost("photos/initiate")]
    [HttpPost("reporting/photos/upload-init")]
    [ReportingProtocol(RequestKind = ReportingProtocolRequestKind.Upload)]
    [ProducesResponseType(typeof(InitiatePhotoUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InitiatePhotoUpload([FromBody] InitiatePhotoUploadRequest request, CancellationToken cancellationToken = default)
    {
        if (!Id<ReportRequest>.TryParse(request.ReportRequestId, out var parsedRequestId))
        {
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.FieldRequired));
        }

        if (!TryParsePhotoBinding(request.PhotoTypeId, request.ReportPhotoSlotId, out var photoTypeId))
        {
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.FieldRequired));
        }

        var result = await _photos.InitiateAsync(
            HttpContext.GetAuthenticatedAccountContext()!,
            new InitiatePhotoUploadCommand
            {
                ReportRequestId = parsedRequestId,
                ViewType = request.ViewType,
                PhotoTypeId = photoTypeId,
                ReportPhotoSlotId = request.ReportPhotoSlotId,
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

    [HttpPost("photos/complete-upload")]
    [HttpPost("reporting/photos/complete-upload")]
    [ReportingProtocol(RequestKind = ReportingProtocolRequestKind.Upload)]
    [ProducesResponseType(typeof(CompletePhotoUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompletePhotoUpload([FromBody] CompletePhotoUploadRequest request, CancellationToken cancellationToken = default)
    {
        if (!Id<ReportRequest>.TryParse(request.ReportRequestId, out var parsedRequestId))
        {
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.FieldRequired));
        }

        if (!TryParsePhotoBinding(request.PhotoTypeId, request.ReportPhotoSlotId, out var photoTypeId))
        {
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.FieldRequired));
        }

        var result = await _photos.CompleteAsync(
            HttpContext.GetAuthenticatedAccountContext()!,
            new CompletePhotoUploadCommand
            {
                StorageKey = request.StorageKey,
                MimeType = request.MimeType,
                SizeBytes = request.SizeBytes,
                Checksum = request.Checksum,
                ReportRequestId = parsedRequestId,
                ViewType = request.ViewType,
                PhotoTypeId = photoTypeId,
                ReportPhotoSlotId = request.ReportPhotoSlotId
            },
            cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<CompletePhotoUploadResult, CompletePhotoUploadResponse>(result.Value));
    }

    private static bool TryParsePhotoBinding(string? photoTypeId, string? reportPhotoSlotId, out Id<TrainerPhotoType>? parsedPhotoTypeId)
    {
        parsedPhotoTypeId = null;
        if (string.IsNullOrWhiteSpace(photoTypeId) && string.IsNullOrWhiteSpace(reportPhotoSlotId))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(photoTypeId)
               && Id<TrainerPhotoType>.TryParse(photoTypeId, out var parsedId)
               && (parsedPhotoTypeId = parsedId).HasValue;
    }

    [HttpGet("reporting/photos/history")]
    [ProducesResponseType(typeof(GetPhotoHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPhotoHistory([FromQuery] string? requestId, CancellationToken cancellationToken = default)
    {
        Id<ReportRequest>? parsedRequestId = null;
        if (!string.IsNullOrWhiteSpace(requestId))
        {
            if (!Id<ReportRequest>.TryParse(requestId, out var tempId))
            {
                return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.FieldRequired));
            }
            parsedRequestId = tempId;
        }

        var result = await _photos.GetHistoryAsync(HttpContext.GetAuthenticatedAccountContext()!, parsedRequestId, cancellationToken);

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
}
