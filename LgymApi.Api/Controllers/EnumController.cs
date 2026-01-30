using System.Globalization;
using LgymApi.Api.DTOs;
using LgymApi.Api.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Controllers;

[ApiController]
[Route("api/enums")]
public sealed class EnumController : ControllerBase
{
    private readonly IEnumLookupService _enumLookupService;

    public EnumController(IEnumLookupService enumLookupService)
    {
        _enumLookupService = enumLookupService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public IActionResult GetAvailableEnums()
    {
        return Ok(_enumLookupService.GetAvailableEnumTypes());
    }

    [HttpGet("all")]
    [ProducesResponseType(typeof(List<EnumLookupResponseDto>), StatusCodes.Status200OK)]
    public IActionResult GetAllEnumLookups()
    {
        var culture = GetRequestCulture();
        var enumTypes = _enumLookupService.GetAvailableEnumTypes();
        var result = new List<EnumLookupResponseDto>();

        foreach (var enumType in enumTypes)
        {
            var lookup = _enumLookupService.GetLookupByName(enumType, culture);
            if (lookup != null)
            {
                result.Add(lookup);
            }
        }

        return Ok(result);
    }

    [HttpGet("{enumType}")]
    [ProducesResponseType(typeof(EnumLookupResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public IActionResult GetEnumLookup([FromRoute] string enumType)
    {
        var culture = GetRequestCulture();
        var lookup = _enumLookupService.GetLookupByName(enumType, culture);

        if (lookup == null)
        {
            return NotFound(new ResponseMessageDto { Message = Messages.DidntFind });
        }

        return Ok(lookup);
    }

    private CultureInfo GetRequestCulture()
    {
        var feature = HttpContext.Features.Get<IRequestCultureFeature>();
        return feature?.RequestCulture?.UICulture ?? CultureInfo.CurrentUICulture;
    }
}
