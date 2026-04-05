using System.Text.Json.Serialization;
using LgymApi.Api.Interfaces;

namespace LgymApi.Api.Features.Public.Contracts;

public sealed class PublicInvitationStatusDto : IResultDto
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("userExists")]
    public bool UserExists { get; set; }
}
