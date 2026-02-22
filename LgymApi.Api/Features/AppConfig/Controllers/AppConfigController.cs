using LgymApi.Api.Features.AppConfig.Contracts;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Application.Features.AppConfig;
using LgymApi.Application.Mapping.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using AppConfigEntity = LgymApi.Domain.Entities.AppConfig;

namespace LgymApi.Api.Features.AppConfig.Controllers;

[ApiController]
[Route("api")]
public sealed class AppConfigController : ControllerBase
{
    private readonly IAppConfigService _appConfigService;
    private readonly IMapper _mapper;

    public AppConfigController(IAppConfigService appConfigService, IMapper mapper)
    {
        _appConfigService = appConfigService;
        _mapper = mapper;
    }

    [HttpPost("appConfig/getAppVersion")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AppConfigInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAppVersion([FromBody] AppConfigVersionRequestDto request)
    {
        var config = await _appConfigService.GetLatestByPlatformAsync(request.Platform, HttpContext.RequestAborted);
        return Ok(_mapper.Map<AppConfigEntity, AppConfigInfoDto>(config));
    }

    [HttpPost("appConfig/createNewAppVersion/{id}")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateNewAppVersion([FromRoute] string id, [FromBody] AppConfigInfoWithPlatformDto form)
    {
        var userId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var input = new CreateAppVersionInput(
            form.Platform,
            form.MinRequiredVersion,
            form.LatestVersion,
            form.ForceUpdate,
            form.UpdateUrl,
            form.ReleaseNotes);
        await _appConfigService.CreateNewAppVersionAsync(userId, input, HttpContext.RequestAborted);
        return StatusCode(StatusCodes.Status201Created, _mapper.Map<string, ResponseMessageDto>(Messages.Created));
    }
}
