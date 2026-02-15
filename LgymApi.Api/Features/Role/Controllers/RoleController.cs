using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Role.Contracts;
using LgymApi.Application.Features.Role;
using LgymApi.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Role.Controllers;

[ApiController]
[Route("api/roles")]
[Authorize(Policy = AuthConstants.Policies.ManageUserRoles)]
public sealed class RoleController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RoleController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<RoleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _roleService.GetRolesAsync();
        return Ok(roles.Select(MapRole).ToList());
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRole([FromRoute] string id)
    {
        var roleId = Guid.TryParse(id, out var parsedRoleId) ? parsedRoleId : Guid.Empty;
        var role = await _roleService.GetRoleAsync(roleId);
        return Ok(MapRole(role));
    }

    [HttpGet("permission-claims")]
    [ProducesResponseType(typeof(List<PermissionClaimLookupDto>), StatusCodes.Status200OK)]
    public IActionResult GetPermissionClaims()
    {
        var claims = _roleService.GetAvailablePermissionClaims();
        return Ok(claims.Select(c => new PermissionClaimLookupDto
        {
            ClaimType = c.ClaimType,
            ClaimValue = c.ClaimValue,
            DisplayName = c.DisplayName
        }).ToList());
    }

    [HttpPost]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateRole([FromBody] UpsertRoleRequest request)
    {
        var role = await _roleService.CreateRoleAsync(request.Name, request.Description, request.PermissionClaims);
        return Ok(MapRole(role));
    }

    [HttpPost("{id}/update")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRole([FromRoute] string id, [FromBody] UpsertRoleRequest request)
    {
        var roleId = Guid.TryParse(id, out var parsedRoleId) ? parsedRoleId : Guid.Empty;
        await _roleService.UpdateRoleAsync(roleId, request.Name, request.Description, request.PermissionClaims);
        return Ok(new ResponseMessageDto { Message = Messages.Updated });
    }

    [HttpPost("{id}/delete")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRole([FromRoute] string id)
    {
        var roleId = Guid.TryParse(id, out var parsedRoleId) ? parsedRoleId : Guid.Empty;
        await _roleService.DeleteRoleAsync(roleId);
        return Ok(new ResponseMessageDto { Message = Messages.Deleted });
    }

    [HttpPost("users/{id}/roles")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUserRoles([FromRoute] string id, [FromBody] UpdateUserRolesRequest request)
    {
        var userId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        await _roleService.UpdateUserRolesAsync(userId, request.Roles);
        return Ok(new ResponseMessageDto { Message = Messages.Updated });
    }

    private static RoleDto MapRole(LgymApi.Application.Features.Role.Models.RoleResult role)
    {
        return new RoleDto
        {
            Id = role.Id.ToString(),
            Name = role.Name,
            Description = role.Description,
            PermissionClaims = role.PermissionClaims
        };
    }
}
