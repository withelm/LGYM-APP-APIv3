using System.Text.Json.Serialization;
using LgymApi.Api.Interfaces;

namespace LgymApi.Api.Features.User.Contracts;

public sealed class RegisterUserRequest : IDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("cpassword")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [JsonPropertyName("isVisibleInRanking")]
    public bool? IsVisibleInRanking { get; set; }
}

public sealed class LoginRequest : IDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}

public sealed class RankDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("needElo")]
    public int NeedElo { get; set; }
}

public sealed class UserInfoDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("avatar")]
    public string? Avatar { get; set; }

    [JsonPropertyName("admin")]
    public bool? Admin { get; set; }

    [JsonPropertyName("profileRank")]
    public string ProfileRank { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("elo")]
    public int Elo { get; set; }

    [JsonPropertyName("nextRank")]
    public RankDto? NextRank { get; set; }

    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; }

    [JsonPropertyName("isTester")]
    public bool IsTester { get; set; }

    [JsonPropertyName("isVisibleInRanking")]
    public bool IsVisibleInRanking { get; set; }
}

public sealed class UserBaseInfoDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("avatar")]
    public string? Avatar { get; set; }

    [JsonPropertyName("elo")]
    public int Elo { get; set; }

    [JsonPropertyName("profileRank")]
    public string ProfileRank { get; set; } = string.Empty;
}

public sealed class UserEloDto
{
    [JsonPropertyName("elo")]
    public int Elo { get; set; }
}

public sealed class LoginResponseDto
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("req")]
    public UserInfoDto? User { get; set; }
}
