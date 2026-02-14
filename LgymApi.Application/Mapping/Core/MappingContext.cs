using System.Collections.Concurrent;

namespace LgymApi.Application.Mapping.Core;

public sealed class MappingContext : IMappingContext
{
    private readonly ConcurrentDictionary<string, object?> _items = new(StringComparer.Ordinal);

    public T? Get<T>(string key)
    {
        if (_items.TryGetValue(key, out var value) && value is T typed)
        {
            return typed;
        }

        return default;
    }

    public void Set<T>(string key, T value)
    {
        _items[key] = value;
    }
}
