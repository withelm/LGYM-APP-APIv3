using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Mapping.Core;
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
    public async Task<IActionResult> GetPendingRequests()
    {
        var trainee = HttpContext.GetCurrentUser();
        var requests = await _reportingService.GetPendingRequestsForTraineeAsync(trainee!);
        return Ok(_mapper.MapList<ReportRequestResult, ReportRequestDto>(requests));
    }

    [HttpPost("report-requests/{requestId}/submit")]
    [ProducesResponseType(typeof(ReportSubmissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitRequest([FromRoute] string requestId, [FromBody] SubmitReportRequestRequest request)
    {
        if (!Guid.TryParse(requestId, out var parsedRequestId))
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var trainee = HttpContext.GetCurrentUser();
        var submission = await _reportingService.SubmitReportRequestAsync(trainee!, parsedRequestId, new SubmitReportRequestCommand
        {
            Answers = new Dictionary<string, System.Text.Json.JsonElement>(request.Answers, StringComparer.OrdinalIgnoreCase)
        });

        return Ok(_mapper.Map<ReportSubmissionResult, ReportSubmissionDto>(submission));
    }
}
