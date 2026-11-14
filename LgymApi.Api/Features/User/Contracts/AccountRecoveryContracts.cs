using System.Text.Json.Serialization;
using LgymApi.Api.Interfaces;

namespace LgymApi.Api.Features.User.Contracts;

public sealed class AccountRecoveryTokenRequest : IDto
{
    [JsonPropertyName("recoveryToken")]
    public string RecoveryToken { get; set; } = string.Empty;
}

public sealed class AccountRecoveryTokenResponse : IResultDto
{
    [JsonPropertyName("recoveryToken")]
    public string RecoveryToken { get; set; } = string.Empty;
}
