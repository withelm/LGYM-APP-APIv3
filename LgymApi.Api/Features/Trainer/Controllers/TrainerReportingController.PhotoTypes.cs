using LgymApi.Api.Extensions;
using LgymApi.Api.Middleware;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using LgymApi.Identity.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Trainer.Controllers;

public sealed partial class TrainerReportingController
{
    [HttpGet("reporting/photo-types")]
    [ReportingProtocol(2)]
    [ProducesResponseType(typeof(List<TrainerPhotoTypeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPhotoTypes(CancellationToken cancellationToken = default)
    {
        var result = await _photoTypes!.GetAllAsync(HttpContext.GetAuthenticatedAccountContext()!, cancellationToken);
        return result.IsFailure ? result.ToActionResult() : Ok(_mapper.MapList<TrainerPhotoTypeResult, TrainerPhotoTypeDto>(result.Value));
    }

    [HttpPost("reporting/photo-types")]
    [ReportingProtocol(2)]
    [ProducesResponseType(typeof(TrainerPhotoTypeDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreatePhotoType([FromBody] CreateTrainerPhotoTypeRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _photoTypes!.CreateAsync(HttpContext.GetAuthenticatedAccountContext()!, request.Name, cancellationToken);
        return result.IsFailure ? result.ToActionResult() : StatusCode(StatusCodes.Status201Created, _mapper.Map<TrainerPhotoTypeResult, TrainerPhotoTypeDto>(result.Value));
    }

    [HttpPost("reporting/photo-types/{photoTypeId}/update")]
    [ReportingProtocol(2)]
    [ProducesResponseType(typeof(TrainerPhotoTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePhotoType([FromRoute] string photoTypeId, [FromBody] UpdateTrainerPhotoTypeRequest request, CancellationToken cancellationToken = default)
    {
        if (!Id<TrainerPhotoType>.TryParse(photoTypeId, out var parsedPhotoTypeId))
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.FieldRequired));
        var result = await _photoTypes!.UpdateAsync(HttpContext.GetAuthenticatedAccountContext()!, parsedPhotoTypeId, request.Name, request.IsArchived, cancellationToken);
        return result.IsFailure ? result.ToActionResult() : Ok(_mapper.Map<TrainerPhotoTypeResult, TrainerPhotoTypeDto>(result.Value));
    }

    [HttpPost("reporting/photo-types/{photoTypeId}/reset-name")]
    [ReportingProtocol(2)]
    [ProducesResponseType(typeof(TrainerPhotoTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPhotoTypeName([FromRoute] string photoTypeId, CancellationToken cancellationToken = default)
    {
        if (!Id<TrainerPhotoType>.TryParse(photoTypeId, out var parsedPhotoTypeId))
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.FieldRequired));
        var result = await _photoTypes!.ResetNameAsync(HttpContext.GetAuthenticatedAccountContext()!, parsedPhotoTypeId, cancellationToken);
        return result.IsFailure ? result.ToActionResult() : Ok(_mapper.Map<TrainerPhotoTypeResult, TrainerPhotoTypeDto>(result.Value));
    }
}
