using System.Text.Json.Serialization;
using LgymApi.Api.Interfaces;
using LgymApi.Domain.Enums;

namespace LgymApi.Api.Features.Trainer.Contracts;

public sealed class CreateTrainerPhotoTypeRequest : IDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class UpdateTrainerPhotoTypeRequest : IDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("isArchived")]
    public bool? IsArchived { get; set; }
}

public sealed class TrainerPhotoTypeDto : IResultDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;
    [JsonPropertyName("systemKey")]
    public string? SystemKey { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("nameMode")]
    public PhotoTypeNameMode NameMode { get; set; }
    [JsonPropertyName("isArchived")]
    public bool IsArchived { get; set; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("labels")]
    public PhotoTypeLabelsDto Labels { get; set; } = new();
}

public sealed class PhotoTypeLabelsDto : IResultDto
{
    [JsonPropertyName("pl")]
    public string Pl { get; set; } = string.Empty;

    [JsonPropertyName("en")]
    public string En { get; set; } = string.Empty;
}
