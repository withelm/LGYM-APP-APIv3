using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Features.DietPlans;
using LgymApi.Application.Features.DietPlans.Models;
using LgymApi.Application.Mapping.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Trainer.Controllers;

[ApiController]
[Route("api/trainee")]
[Authorize]
public sealed class TraineeDietPlanController : ControllerBase
{
    private readonly IDietPlanService _dietPlanService;
    private readonly IMapper _mapper;

    public TraineeDietPlanController(IDietPlanService dietPlanService, IMapper mapper)
    {
        _dietPlanService = dietPlanService;
        _mapper = mapper;
    }

    [HttpGet("diet-plans/current")]
    [ProducesResponseType(typeof(List<DietPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentPlans(CancellationToken cancellationToken = default)
    {
        var trainee = HttpContext.GetCurrentUser();
        var result = await _dietPlanService.GetCurrentPlansAsync(trainee!, cancellationToken);
        return result.IsFailure ? result.ToActionResult() : Ok(_mapper.MapList<DietPlanResult, DietPlanDto>(result.Value));
    }

    [HttpGet("diet-plan/current")]
    [ProducesResponseType(typeof(DietPlanDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentPlan(CancellationToken cancellationToken = default)
    {
        var trainee = HttpContext.GetCurrentUser();
        var result = await _dietPlanService.GetCurrentPlanAsync(trainee!, cancellationToken);
        return result.IsFailure ? result.ToActionResult() : Ok(_mapper.Map<DietPlanResult, DietPlanDto>(result.Value));
    }
}
