using System.Text.Json.Serialization;
using LgymApi.Api.Interfaces;

namespace LgymApi.Api.Features.Plan.Contracts;

public sealed class PlanFormDto : IDto, IResultDto
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("isActive")]
    public bool? IsActive { get; set; }
}

public sealed record CopyPlanDto(
    [property: JsonPropertyName("shareCode")] string ShareCode
) : IDto;

public sealed class PlanDto : IResultDto
{
    [JsonPropertyName("id")]
    public LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Plan> Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("userId")]
    public LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User> UserId { get; set; }
}

public sealed record ShareCodeResponseDto(
    [property: JsonPropertyName("shareCode")] string ShareCode
) : IResultDto;

public sealed class SetActivePlanDto : IDto
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }
}


