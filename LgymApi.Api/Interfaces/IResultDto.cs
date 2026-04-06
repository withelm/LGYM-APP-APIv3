using System.Text.Json.Serialization;

namespace LgymApi.Api.Interfaces
{
    public interface IResultDto;

    public abstract class PaginatedResponse<TItem>
        where TItem : IResultDto
    {
        [JsonPropertyName("items")]
        public List<TItem> Items { get; set; } = [];

        [JsonPropertyName("page")]
        public int Page { get; set; }

        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; }

        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }

        [JsonPropertyName("totalPages")]
        public int TotalPages { get; set; }

        [JsonPropertyName("hasNextPage")]
        public bool HasNextPage { get; set; }

        [JsonPropertyName("hasPreviousPage")]
        public bool HasPreviousPage { get; set; }
    }
}
