namespace LgymApi.Notifications.Application.Models;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    bool HasNextPage,
    DateTimeOffset? NextCursorCreatedAt,
    Guid? NextCursorId);
