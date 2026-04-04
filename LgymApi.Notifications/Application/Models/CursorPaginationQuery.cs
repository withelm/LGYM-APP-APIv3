namespace LgymApi.Notifications.Application.Models;

public sealed record CursorPaginationQuery(
    int Limit = 20,
    DateTimeOffset? CursorCreatedAt = null,
    Guid? CursorId = null);
