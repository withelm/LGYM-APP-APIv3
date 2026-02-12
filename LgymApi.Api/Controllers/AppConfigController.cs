using LgymApi.Api.DTOs;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace LgymApi.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class AppConfigController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IAppConfigRepository _appConfigRepository;

    public AppConfigController(IUserRepository userRepository, IAppConfigRepository appConfigRepository)
    {
        _userRepository = userRepository;
        _appConfigRepository = appConfigRepository;
    }

    [HttpPost("appConfig/getAppVersion")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AppConfigInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAppVersion([FromBody] Dictionary<string, string> body)
    {
        if (!body.TryGetValue("platform", out var platformRaw))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        if (!Enum.TryParse(platformRaw, true, out Platforms platform))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var config = await _appConfigRepository.GetLatestByPlatformAsync(platform);

        if (config == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        return Ok(new AppConfigInfoDto
        {
            MinRequiredVersion = config.MinRequiredVersion,
            LatestVersion = config.LatestVersion,
            ForceUpdate = config.ForceUpdate,
            UpdateUrl = config.UpdateUrl,
            ReleaseNotes = config.ReleaseNotes
        });
    }

    [HttpPost("appConfig/createNewAppVersion/{id}")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateNewAppVersion([FromRoute] string id, [FromBody] AppConfigInfoWithPlatformDto form)
    {
        if (!Guid.TryParse(id, out var userId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ResponseMessageDto { Message = Messages.Forbidden });
        }

        var user = await _userRepository.FindByIdAsync(userId);
        if (user == null || user.Admin != true)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ResponseMessageDto { Message = Messages.Forbidden });
        }

        if (string.IsNullOrWhiteSpace(form.Platform))
        {
            return StatusCode(StatusCodes.Status400BadRequest, new ResponseMessageDto { Message = Messages.FieldRequired });
        }

        if (!Enum.TryParse(form.Platform, true, out Platforms platform))
        {
            return StatusCode(StatusCodes.Status400BadRequest, new ResponseMessageDto { Message = Messages.FieldRequired });
        }

        var config = new AppConfig
        {
            Id = Guid.NewGuid(),
            Platform = platform,
            MinRequiredVersion = form.MinRequiredVersion,
            LatestVersion = form.LatestVersion,
            ForceUpdate = form.ForceUpdate,
            UpdateUrl = form.UpdateUrl,
            ReleaseNotes = form.ReleaseNotes
        };

        await _appConfigRepository.AddAsync(config);
        return StatusCode(StatusCodes.Status201Created, new ResponseMessageDto { Message = Messages.Created });
    }
}
