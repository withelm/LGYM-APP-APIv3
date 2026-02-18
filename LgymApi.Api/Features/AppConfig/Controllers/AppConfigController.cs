using LgymApi.Api.Features.AppConfig.Contracts;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Application.Features.AppConfig;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace LgymApi.Api.Features.AppConfig.Controllers;

[ApiController]
[Route("api")]
public sealed class AppConfigController : ControllerBase
{
    private readonly IAppConfigService _appConfigService;

    public AppConfigController(IAppConfigService appConfigService)
    {
        _appConfigService = appConfigService;
    }

    [HttpPost("appConfig/getAppVersion")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AppConfigInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAppVersion([FromBody] AppConfigPlatformRequestDto request)
    {
        var config = await _appConfigService.GetLatestByPlatformAsync(request.Platform);

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
        var userId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        await _appConfigService.CreateNewAppVersionAsync(
            userId,
            form.Platform,
            form.MinRequiredVersion,
            form.LatestVersion,
            form.ForceUpdate,
            form.UpdateUrl,
            form.ReleaseNotes);
        return StatusCode(StatusCodes.Status201Created, new ResponseMessageDto { Message = Messages.Created });
    }
}
