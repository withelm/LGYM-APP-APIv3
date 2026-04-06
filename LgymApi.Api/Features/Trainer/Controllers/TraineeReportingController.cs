using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Trainer.Controllers;

[ApiController]
[Route("api/trainee")]
[Authorize]
public sealed class TraineeReportingController : ControllerBase
{
    private readonly IReportingService _reportingService;
    private readonly IMapper _mapper;

    public TraineeReportingController(IReportingService reportingService, IMapper mapper)
    {
        _reportingService = reportingService;
        _mapper = mapper;
    }

    [HttpGet("report-requests")]
    [ProducesResponseType(typeof(List<ReportRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingRequests(CancellationToken cancellationToken = default)
    {
        var trainee = HttpContext.GetCurrentUser();
        var result = await _reportingService.GetPendingRequestsForTraineeAsync(trainee!, cancellationToken);

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

        var trainee = HttpContext.GetCurrentUser();
        var result = await _reportingService.SubmitReportRequestAsync(trainee!, parsedRequestId, new SubmitReportRequestCommand
        {
            Answers = new Dictionary<string, System.Text.Json.JsonElement>(request.Answers, StringComparer.OrdinalIgnoreCase)
        }, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<ReportSubmissionResult, ReportSubmissionDto>(result.Value));
    }
}
