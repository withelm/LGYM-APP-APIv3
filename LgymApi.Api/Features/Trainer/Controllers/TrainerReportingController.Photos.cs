using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using Microsoft.AspNetCore.Mvc;
using ReportRequestEntity = LgymApi.Domain.Entities.ReportRequest;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Api.Features.Trainer.Controllers;

public sealed partial class TrainerReportingController
{
    [HttpPost("reporting/photos/upload-init")]
    [ProducesResponseType(typeof(InitiatePhotoUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InitiatePhotoUpload([FromBody] InitiatePhotoUploadRequest request, CancellationToken cancellationToken = default)
    {
        if (!Id<ReportRequestEntity>.TryParse(request.ReportRequestId, out var parsedRequestId))
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
        if (!Id<ReportRequestEntity>.TryParse(request.ReportRequestId, out var parsedRequestId))
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
        Id<UserEntity>? parsedTraineeId = null;
        if (!string.IsNullOrWhiteSpace(traineeId))
        {
            if (!Id<UserEntity>.TryParse(traineeId, out var tempId))
            {
                return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.FieldRequired));
            }

            parsedTraineeId = tempId;
        }

        Id<ReportRequestEntity>? parsedRequestId = null;
        if (!string.IsNullOrWhiteSpace(requestId))
        {
            if (!Id<ReportRequestEntity>.TryParse(requestId, out var tempId))
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

        return Ok(new GetPhotoHistoryResponse
        {
            Photos = _mapper.MapList<PhotoHistoryItemResult, PhotoHistoryItemResponse>(result.Value)
        });
    }
}
