using System.Text.Json.Serialization;
using LgymApi.Api.Interfaces;

namespace LgymApi.Api.Features.Enum.Contracts;

public sealed class EnumLookupDto : IResultDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class EnumLookupResponseDto : IResultDto
{
    [JsonPropertyName("enumType")]
    public string EnumType { get; set; } = string.Empty;

    [JsonPropertyName("values")]
    public List<EnumLookupDto> Values { get; set; } = new();
}
