using System.Text.Json.Serialization;
using LgymApi.Api.Interfaces;

namespace LgymApi.Api.Features.Trainer.Contracts;

public sealed class ReportingCapabilitiesResponse : IResultDto
{
    [JsonPropertyName("supportedProtocols")]
    public int[] SupportedProtocols { get; init; } = [];

    [JsonPropertyName("photoTypesEnabled")]
    public bool PhotoTypesEnabled { get; init; }
}

public sealed class ReportingProtocolErrorResponse : IResultDto
{
    [JsonPropertyName("msg")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;
}
