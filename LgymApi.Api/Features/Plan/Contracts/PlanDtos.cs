using System.Text.Json.Serialization;

namespace LgymApi.Api.Features.Plan.Contracts;

public sealed class PlanFormDto
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
);

public sealed class PlanDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("userId")]
    public Guid UserId { get; set; }
}

public sealed record ShareCodeResponseDto(
    [property: JsonPropertyName("shareCode")] string ShareCode
);

public sealed class SetActivePlanDto
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }
}


