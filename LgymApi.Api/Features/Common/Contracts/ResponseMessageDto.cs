using System.Text.Json.Serialization;
using LgymApi.Api.Interfaces;

namespace LgymApi.Api.Features.Common.Contracts;

public sealed class ResponseMessageDto : IResultDto
{
    [JsonPropertyName("msg")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("isNew")]
    public bool? IsNew { get; set; }
}
