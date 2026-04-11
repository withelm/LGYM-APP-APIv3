using System.Text.Json.Serialization;
using LgymApi.Api.Interfaces;

namespace LgymApi.Api.Features.User.Contracts;

public sealed class ChangeVisibilityInRankingRequest : IDto
{
    [JsonPropertyName("isVisibleInRanking")]
    public bool? IsVisibleInRanking { get; set; }
}
