using System.Globalization;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Enum.Contracts;
using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.Enum;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Enum.Controllers;

[ApiController]
[Route("api/enums")]
public sealed class EnumController : ControllerBase
{
    private readonly IEnumService _enumService;

    public EnumController(IEnumService enumService)
    {
        _enumService = enumService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public IActionResult GetAvailableEnums()
    {
        return Ok(_enumService.GetAvailableEnumTypes());
    }

    [HttpGet("all")]
    [ProducesResponseType(typeof(List<EnumLookupResponseDto>), StatusCodes.Status200OK)]
    public IActionResult GetAllEnumLookups()
    {
        var culture = GetRequestCulture();
        var enumTypes = _enumService.GetAvailableEnumTypes();
        var result = new List<EnumLookupResponseDto>();

        foreach (var enumType in enumTypes)
        {
            var lookup = _enumService.GetLookupByName(enumType, culture);
            if (lookup != null)
            {
                result.Add(new EnumLookupResponseDto
                {
                    EnumType = lookup.EnumType,
                    Values = lookup.Values.Select(value => new EnumLookupDto
                    {
                        Name = value.Name,
                        DisplayName = value.DisplayName
                    }).ToList()
                });
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
        var lookup = _enumService.GetLookupByName(enumType, culture);

        if (lookup == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return Ok(new EnumLookupResponseDto
        {
            EnumType = lookup.EnumType,
            Values = lookup.Values.Select(value => new EnumLookupDto
            {
                Name = value.Name,
                DisplayName = value.DisplayName
            }).ToList()
        });
    }

    private CultureInfo GetRequestCulture()
    {
        var feature = HttpContext.Features.Get<IRequestCultureFeature>();
        return feature?.RequestCulture?.UICulture ?? CultureInfo.CurrentUICulture;
    }
}
