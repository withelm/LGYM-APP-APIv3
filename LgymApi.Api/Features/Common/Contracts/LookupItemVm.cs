using System.Text.Json.Serialization;
using LgymApi.Api.Interfaces;
using LgymApi.Api.Features.Common.Serialization;

namespace LgymApi.Api.Features.Common.Contracts;

// Accepts both the legacy string shape and the new lookup-item object shape.
[JsonConverter(typeof(LookupItemVmJsonConverter))]
public sealed class LookupItemVm : IDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
}
