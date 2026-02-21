using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Role.Contracts;
using LgymApi.Application.Features.Role;
using LgymApi.Application.Features.Role.Models;
using LgymApi.Application.Mapping.Core;
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
        var roles = await _roleService.GetRolesAsync();
        return Ok(_mapper.MapList<RoleResult, RoleDto>(roles));
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRole([FromRoute] string id)
    {
        var roleId = Guid.TryParse(id, out var parsedRoleId) ? parsedRoleId : Guid.Empty;
        var role = await _roleService.GetRoleAsync(roleId);
        return Ok(_mapper.Map<RoleResult, RoleDto>(role));
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
        var role = await _roleService.CreateRoleAsync(request.Name, request.Description, request.PermissionClaims);
        return Ok(_mapper.Map<RoleResult, RoleDto>(role));
    }

    [HttpPost("{id}/update")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRole([FromRoute] string id, [FromBody] UpsertRoleRequest request)
    {
        var roleId = Guid.TryParse(id, out var parsedRoleId) ? parsedRoleId : Guid.Empty;
        await _roleService.UpdateRoleAsync(roleId, request.Name, request.Description, request.PermissionClaims);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpPost("{id}/delete")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRole([FromRoute] string id)
    {
        var roleId = Guid.TryParse(id, out var parsedRoleId) ? parsedRoleId : Guid.Empty;
        await _roleService.DeleteRoleAsync(roleId);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Deleted));
    }

    [HttpPost("users/{id}/roles")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUserRoles([FromRoute] string id, [FromBody] UpdateUserRolesRequest request)
    {
        var userId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        await _roleService.UpdateUserRolesAsync(userId, request.Roles);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

}
