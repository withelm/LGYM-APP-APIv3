using System.Collections.Concurrent;

namespace LgymApi.Application.Mapping.Core;

public sealed class MappingContext : IMappingContext
{
    private readonly ConcurrentDictionary<ContextKey<object>, object?> _items = new();
    private readonly IReadOnlySet<string>? _allowedKeys;

    public MappingContext(IReadOnlySet<string>? allowedKeys = null)
    {
        _allowedKeys = allowedKeys;
    }

    public T? Get<T>(ContextKey<T> key)
    {
        if (_items.TryGetValue(new ContextKey<object>(key.Name), out var value) && value is T typed)
        {
            return typed;
        }

        return default;
    }

    public void Set<T>(ContextKey<T> key, T value)
    {
        if (_allowedKeys != null && !_allowedKeys.Contains(key.Name))
        {
            throw new InvalidOperationException($"Context key '{key.Name}' is not allowed. Register it via MappingConfiguration.AllowContextKey.");
        }

        _items[new ContextKey<object>(key.Name)] = value;
    }
}
