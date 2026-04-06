using System.Text.Json.Serialization;
using LgymApi.Api.Interfaces;

namespace LgymApi.Api.Features.AdminManagement.Contracts;

public sealed class AdminUserDto : IResultDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("avatar")]
    public string? Avatar { get; set; }

    [JsonPropertyName("profileRank")]
    public string ProfileRank { get; set; } = string.Empty;

    [JsonPropertyName("isVisibleInRanking")]
    public bool IsVisibleInRanking { get; set; }

    [JsonPropertyName("isBlocked")]
    public bool IsBlocked { get; set; }

    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("roles")]
    public List<string> Roles { get; set; } = new();
}

public sealed class UpdateUserRequest : IDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("profileRank")]
    public string ProfileRank { get; set; } = string.Empty;

    [JsonPropertyName("isVisibleInRanking")]
    public bool IsVisibleInRanking { get; set; }

    [JsonPropertyName("avatar")]
    public string? Avatar { get; set; }
}

public sealed class PaginatedAdminUserResult : IResultDto
{
    [JsonPropertyName("items")]
    public List<AdminUserDto> Items { get; set; } = new();

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
