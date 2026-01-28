using System.Globalization;
using LgymApi.Api.DTOs;
using LgymApi.Application.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class EloRegistryController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IEloRegistryRepository _eloRepository;

    public EloRegistryController(IUserRepository userRepository, IEloRegistryRepository eloRepository)
    {
        _userRepository = userRepository;
        _eloRepository = eloRepository;
    }

    [HttpGet("eloRegistry/{id}/getEloRegistryChart")]
    public async Task<IActionResult> GetEloRegistryChart([FromRoute] string id)
    {
        if (!Guid.TryParse(id, out var userId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var user = await _userRepository.FindByIdAsync(userId);
        if (user == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var eloRegistry = await _eloRepository.GetByUserIdAsync(user.Id);

        if (eloRegistry.Count == 0)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var result = eloRegistry.Select(entry => new EloRegistryBaseChartDto
        {
            Id = entry.Id.ToString(),
            Value = entry.Elo,
            Date = entry.Date.UtcDateTime.ToString("MM/dd", CultureInfo.InvariantCulture)
        }).ToList();

        return Ok(result);
    }
}
