using LgymApi.Domain.ValueObjects;

namespace LgymApi.Api.Middleware;

public static class IdParsingExtensions
{
    public static Id<TEntity> ToIdOrEmpty<TEntity>(this string? raw)
    {
        return !string.IsNullOrWhiteSpace(raw) && Id<TEntity>.TryParse(raw, out var parsedId)
            ? parsedId
            : Id<TEntity>.Empty;
    }

    public static Id<TEntity>? ToNullableId<TEntity>(this string? raw)
    {
        return !string.IsNullOrWhiteSpace(raw) && Id<TEntity>.TryParse(raw, out var parsedId)
            ? parsedId
            : null;
    }
}
