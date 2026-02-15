using System.Text.Json.Serialization;

namespace LgymApi.Api.Features.Role.Contracts;

public sealed class PermissionClaimLookupDto
{
    [JsonPropertyName("claimType")]
    public string ClaimType { get; set; } = string.Empty;

    [JsonPropertyName("claimValue")]
    public string ClaimValue { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class RoleDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("permissionClaims")]
    public List<string> PermissionClaims { get; set; } = new();
}

public sealed class UpsertRoleRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("permissionClaims")]
    public List<string> PermissionClaims { get; set; } = new();
}

public sealed class UpdateUserRolesRequest
{
    [JsonPropertyName("roles")]
    public List<string> Roles { get; set; } = new();
}
