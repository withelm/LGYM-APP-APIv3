using System.Globalization;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.EloRegistry.Contracts;
using LgymApi.Application.Features.EloRegistry;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.EloRegistry.Controllers;

[ApiController]
[Route("api")]
public sealed class EloRegistryController : ControllerBase
{
    private readonly IEloRegistryService _eloRegistryService;

    public EloRegistryController(IEloRegistryService eloRegistryService)
    {
        _eloRegistryService = eloRegistryService;
    }

    [HttpGet("eloRegistry/{id}/getEloRegistryChart")]
    [ProducesResponseType(typeof(List<EloRegistryBaseChartDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEloRegistryChart([FromRoute] string id)
    {
        var userId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var result = await _eloRegistryService.GetChartAsync(userId);
        var mapped = result.Select(entry => new EloRegistryBaseChartDto
        {
            Id = entry.Id,
            Value = entry.Value,
            Date = entry.Date
        }).ToList();
        return Ok(mapped);
    }
}
