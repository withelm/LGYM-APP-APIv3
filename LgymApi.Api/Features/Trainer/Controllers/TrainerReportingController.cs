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
