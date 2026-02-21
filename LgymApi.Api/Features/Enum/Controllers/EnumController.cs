using System.Globalization;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Enum.Contracts;
using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.Enum;
using LgymApi.Application.Features.Enum.Models;
using LgymApi.Application.Mapping.Core;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Enum.Controllers;

[ApiController]
[Route("api/enums")]
public sealed class EnumController : ControllerBase
{
    private readonly IEnumService _enumService;
    private readonly IMapper _mapper;

    public EnumController(IEnumService enumService, IMapper mapper)
    {
        _enumService = enumService;
        _mapper = mapper;
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
                result.Add(_mapper.Map<EnumLookupResponse, EnumLookupResponseDto>(lookup));
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

        return Ok(_mapper.Map<EnumLookupResponse, EnumLookupResponseDto>(lookup));
    }

    private CultureInfo GetRequestCulture()
    {
        var feature = HttpContext.Features.Get<IRequestCultureFeature>();
        return feature?.RequestCulture?.UICulture ?? CultureInfo.CurrentUICulture;
    }
}
