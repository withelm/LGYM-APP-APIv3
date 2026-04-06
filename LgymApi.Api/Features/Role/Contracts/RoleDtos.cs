using System.Text.Json.Serialization;
using LgymApi.Api.Interfaces;

namespace LgymApi.Api.Features.Role.Contracts;

public sealed class PermissionClaimLookupDto : IResultDto
{
    [JsonPropertyName("claimType")]
    public string ClaimType { get; set; } = string.Empty;

    [JsonPropertyName("claimValue")]
    public string ClaimValue { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class RoleDto : IResultDto
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

public sealed class UpsertRoleRequest : IDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("permissionClaims")]
    public List<string> PermissionClaims { get; set; } = new();
}

public sealed class UpdateUserRolesRequest : IDto
{
    [JsonPropertyName("roles")]
    public List<string> Roles { get; set; } = new();
}

public sealed class PaginatedRoleResult : IResultDto
{
    [JsonPropertyName("items")]
    public List<RoleDto> Items { get; set; } = new();

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }

    [JsonPropertyName("hasNextPage")]
    public bool HasNextPage { get; set; }

    [JsonPropertyName("hasPreviousPage")]
    public bool HasPreviousPage { get; set; }
}
