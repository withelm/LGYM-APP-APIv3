using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Tutorial.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Features.Tutorial;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Tutorial.Controllers;

[ApiController]
[Route("api/tutorials")]
public sealed class TutorialController : ControllerBase
{
    private readonly ITutorialService _tutorialService;
    private readonly IMapper _mapper;

    public TutorialController(ITutorialService tutorialService, IMapper mapper)
    {
        _tutorialService = tutorialService;
        _mapper = mapper;
    }

    [HttpGet("active")]
    [ProducesResponseType(typeof(List<TutorialProgressDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveTutorials()
    {
        var user = HttpContext.GetCurrentUser();
        var result = await _tutorialService.GetActiveTutorialsAsync((Guid)user!.Id, HttpContext.RequestAborted);
        var mapped = _mapper.MapList<Application.Features.Tutorial.Models.TutorialProgressResult, TutorialProgressDto>(result);
        return Ok(mapped);
    }

    [HttpGet("{tutorialType}")]
    [ProducesResponseType(typeof(TutorialProgressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTutorialProgress([FromRoute] TutorialType tutorialType)
    {
        var user = HttpContext.GetCurrentUser();
        var result = await _tutorialService.GetTutorialProgressAsync((Guid)user!.Id, tutorialType, HttpContext.RequestAborted);
        if (result == null)
        {
            return NotFound();
        }

        var mapped = _mapper.Map<Application.Features.Tutorial.Models.TutorialProgressResult, TutorialProgressDto>(result);
        return Ok(mapped);
    }

    [HttpPost("completeStep")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteStep([FromBody] CompleteStepRequest request)
    {
        var user = HttpContext.GetCurrentUser();
        await _tutorialService.CompleteStepAsync((Guid)user!.Id, request.TutorialType, request.Step, HttpContext.RequestAborted);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpPost("complete")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteTutorial([FromBody] CompleteTutorialRequest request)
    {
        var user = HttpContext.GetCurrentUser();
        await _tutorialService.CompleteTutorialAsync((Guid)user!.Id, request.TutorialType, HttpContext.RequestAborted);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }
}
