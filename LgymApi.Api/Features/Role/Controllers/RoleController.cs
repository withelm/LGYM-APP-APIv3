using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Role.Contracts;
using LgymApi.Application.Features.Role;
using LgymApi.Application.Features.Role.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Pagination;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Role.Controllers;

[ApiController]
[Route("api/roles")]
[Authorize(Policy = AuthConstants.Policies.ManageUserRoles)]
public sealed class RoleController : ControllerBase
{
    private readonly IRoleService _roleService;
    private readonly IMapper _mapper;

    public RoleController(IRoleService roleService, IMapper mapper)
    {
        _roleService = roleService;
        _mapper = mapper;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<RoleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoles()
    {
        var cancellationToken = HttpContext?.RequestAborted ?? CancellationToken.None;
        var result = await _roleService.GetRolesAsync(cancellationToken);
        
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }
        
        return Ok(_mapper.MapList<RoleResult, RoleDto>(result.Value));
    }

    [HttpGet("paginated")]
    [ProducesResponseType(typeof(PaginatedRoleResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRolesPaginated([FromQuery] FilterInput filterInput)
    {
        var cancellationToken = HttpContext?.RequestAborted ?? CancellationToken.None;
        var result = await _roleService.GetRolesPaginatedAsync(filterInput, cancellationToken);
        
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        var pagination = result.Value;
        var response = new PaginatedRoleResult
        {
            Items = _mapper.MapList<RoleResult, RoleDto>(pagination.Items),
            Page = pagination.Page,
            PageSize = pagination.PageSize,
            TotalCount = pagination.TotalCount,
            TotalPages = pagination.TotalPages,
            HasNextPage = pagination.HasNextPage,
            HasPreviousPage = pagination.HasPreviousPage
        };
        
        return Ok(response);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRole([FromRoute] string id)
    {
        var cancellationToken = HttpContext?.RequestAborted ?? CancellationToken.None;
        var roleId = Id<Domain.Entities.Role>.TryParse(id, out var parsedRoleId) ? parsedRoleId : Id<Domain.Entities.Role>.Empty;
        var result = await _roleService.GetRoleAsync(roleId, cancellationToken);
        
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }
        
        return Ok(_mapper.Map<RoleResult, RoleDto>(result.Value));
    }

    [HttpGet("permission-claims")]
    [ProducesResponseType(typeof(List<PermissionClaimLookupDto>), StatusCodes.Status200OK)]
    public IActionResult GetPermissionClaims()
    {
        var claims = _roleService.GetAvailablePermissionClaims();
        return Ok(_mapper.MapList<PermissionClaimLookupResult, PermissionClaimLookupDto>(claims));
    }

    [HttpPost]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateRole([FromBody] UpsertRoleRequest request)
    {
        var cancellationToken = HttpContext?.RequestAborted ?? CancellationToken.None;
        var result = await _roleService.CreateRoleAsync(request.Name, request.Description, request.PermissionClaims, cancellationToken);
        
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }
        
        return Ok(_mapper.Map<RoleResult, RoleDto>(result.Value));
    }

    [HttpPost("{id}/update")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRole([FromRoute] string id, [FromBody] UpsertRoleRequest request)
    {
        var cancellationToken = HttpContext?.RequestAborted ?? CancellationToken.None;
        var roleId = Id<Domain.Entities.Role>.TryParse(id, out var parsedRoleId) ? parsedRoleId : Id<Domain.Entities.Role>.Empty;
        var result = await _roleService.UpdateRoleAsync(roleId, request.Name, request.Description, request.PermissionClaims, cancellationToken);
        
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }
        
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpPost("{id}/delete")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRole([FromRoute] string id)
    {
        var cancellationToken = HttpContext?.RequestAborted ?? CancellationToken.None;
        var roleId = Id<Domain.Entities.Role>.TryParse(id, out var parsedRoleId) ? parsedRoleId : Id<Domain.Entities.Role>.Empty;
        var result = await _roleService.DeleteRoleAsync(roleId, cancellationToken);
        
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }
        
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Deleted));
    }

    [HttpPost("users/{id}/roles")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUserRoles([FromRoute] string id, [FromBody] UpdateUserRolesRequest request)
    {
        var cancellationToken = HttpContext?.RequestAborted ?? CancellationToken.None;
        var userId = Id<Domain.Entities.User>.TryParse(id, out var parsedUserId) ? parsedUserId : Id<Domain.Entities.User>.Empty;
        var result = await _roleService.UpdateUserRolesAsync(userId, request.Roles, cancellationToken);
        
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }
        
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

}
