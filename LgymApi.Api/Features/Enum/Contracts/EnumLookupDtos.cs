using System.Text.Json.Serialization;

namespace LgymApi.Api.Features.Enum.Contracts;

public sealed class EnumLookupDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class EnumLookupResponseDto
{
    [JsonPropertyName("enumType")]
    public string EnumType { get; set; } = string.Empty;

    [JsonPropertyName("values")]
    public List<EnumLookupDto> Values { get; set; } = new();
}
