using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Security;
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
    public async Task<IActionResult> CreateTemplate([FromBody] UpsertReportTemplateRequest request)
    {
        var trainer = HttpContext.GetCurrentUser();
        var template = await _reportingService.CreateTemplateAsync(trainer!, new CreateReportTemplateCommand
        {
            Name = request.Name,
            Description = request.Description,
            Fields = request.Fields.Select(MapField).ToList()
        });

        return StatusCode(StatusCodes.Status201Created, _mapper.Map<ReportTemplateResult, ReportTemplateDto>(template));
    }

    [HttpGet("report-templates")]
    [ProducesResponseType(typeof(List<ReportTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTemplates()
    {
        var trainer = HttpContext.GetCurrentUser();
        var templates = await _reportingService.GetTrainerTemplatesAsync(trainer!);
        return Ok(_mapper.MapList<ReportTemplateResult, ReportTemplateDto>(templates));
    }

    [HttpGet("report-templates/{templateId}")]
    [ProducesResponseType(typeof(ReportTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTemplate([FromRoute] string templateId)
    {
        if (!Guid.TryParse(templateId, out var parsedTemplateId))
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var trainer = HttpContext.GetCurrentUser();
        var template = await _reportingService.GetTrainerTemplateAsync(trainer!, parsedTemplateId);
        return Ok(_mapper.Map<ReportTemplateResult, ReportTemplateDto>(template));
    }

    [HttpPost("report-templates/{templateId}/update")]
    [ProducesResponseType(typeof(ReportTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateTemplate([FromRoute] string templateId, [FromBody] UpsertReportTemplateRequest request)
    {
        if (!Guid.TryParse(templateId, out var parsedTemplateId))
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var trainer = HttpContext.GetCurrentUser();
        var template = await _reportingService.UpdateTemplateAsync(trainer!, parsedTemplateId, new CreateReportTemplateCommand
        {
            Name = request.Name,
            Description = request.Description,
            Fields = request.Fields.Select(MapField).ToList()
        });

        return Ok(_mapper.Map<ReportTemplateResult, ReportTemplateDto>(template));
    }

    [HttpPost("report-templates/{templateId}/delete")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteTemplate([FromRoute] string templateId)
    {
        if (!Guid.TryParse(templateId, out var parsedTemplateId))
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var trainer = HttpContext.GetCurrentUser();
        await _reportingService.DeleteTemplateAsync(trainer!, parsedTemplateId);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Deleted));
    }

    [HttpPost("trainees/{traineeId}/report-requests")]
    [ProducesResponseType(typeof(ReportRequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateReportRequest([FromRoute] string traineeId, [FromBody] CreateReportRequestRequest request)
    {
        if (!Guid.TryParse(traineeId, out var parsedTraineeId))
        {
            throw AppException.BadRequest(Messages.UserIdRequired);
        }

        if (!Guid.TryParse(request.TemplateId, out var parsedTemplateId))
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _reportingService.CreateReportRequestAsync(trainer!, parsedTraineeId, new CreateReportRequestCommand
        {
            TemplateId = parsedTemplateId,
            DueAt = request.DueAt,
            Note = request.Note
        });

        return StatusCode(StatusCodes.Status201Created, _mapper.Map<ReportRequestResult, ReportRequestDto>(result));
    }

    [HttpGet("trainees/{traineeId}/report-submissions")]
    [ProducesResponseType(typeof(List<ReportSubmissionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTraineeSubmissions([FromRoute] string traineeId)
    {
        if (!Guid.TryParse(traineeId, out var parsedTraineeId))
        {
            throw AppException.BadRequest(Messages.UserIdRequired);
        }

        var trainer = HttpContext.GetCurrentUser();
        var submissions = await _reportingService.GetTraineeSubmissionsAsync(trainer!, parsedTraineeId);
        return Ok(_mapper.MapList<ReportSubmissionResult, ReportSubmissionDto>(submissions));
    }

    private static ReportTemplateFieldCommand MapField(ReportTemplateFieldRequest field)
    {
        return new ReportTemplateFieldCommand
        {
            Key = field.Key,
            Label = field.Label,
            Type = field.Type,
            IsRequired = field.IsRequired,
            Order = field.Order
        };
    }
}
