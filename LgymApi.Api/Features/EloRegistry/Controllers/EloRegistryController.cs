using System.Globalization;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.EloRegistry.Contracts;
using LgymApi.Application.Features.EloRegistry;
using LgymApi.Application.Features.EloRegistry.Models;
using LgymApi.Application.Mapping.Core;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.EloRegistry.Controllers;

[ApiController]
[Route("api")]
public sealed class EloRegistryController : ControllerBase
{
    private readonly IEloRegistryService _eloRegistryService;
    private readonly IMapper _mapper;

    public EloRegistryController(IEloRegistryService eloRegistryService, IMapper mapper)
    {
        _eloRegistryService = eloRegistryService;
        _mapper = mapper;
    }

    [HttpGet("eloRegistry/{id}/getEloRegistryChart")]
    [ProducesResponseType(typeof(List<EloRegistryBaseChartDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEloRegistryChart([FromRoute] string id)
    {
        var userId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var result = await _eloRegistryService.GetChartAsync(userId, HttpContext.RequestAborted);
        var mapped = _mapper.MapList<EloRegistryChartEntry, EloRegistryBaseChartDto>(result);
        return Ok(mapped);
    }
}
