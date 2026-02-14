using System.Text.Json.Serialization;

namespace LgymApi.Api.Features.Common.Contracts;

public sealed class ResponseMessageDto
{
    [JsonPropertyName("msg")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("isNew")]
    public bool? IsNew { get; set; }
}
