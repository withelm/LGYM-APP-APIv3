using System.Collections.ObjectModel;

namespace LgymApi.Application.Mapping.Core;

public sealed class MappingConfiguration
{
    private readonly Dictionary<(Type Source, Type Target), Func<object, MappingContext?, object>> _mappings = new();

    public void CreateMap<TSource, TTarget>(Func<TSource, MappingContext?, TTarget> map)
    {
        var key = (Source: typeof(TSource), Target: typeof(TTarget));

        if (_mappings.ContainsKey(key))
        {
            throw new InvalidOperationException($"Mapping from {key.Source.Name} to {key.Target.Name} is already registered.");
        }

        _mappings[key] = (source, context) => map((TSource)source, context);
    }

    internal bool TryGetMapping((Type Source, Type Target) key, out Func<object, MappingContext?, object>? mapper)
    {
        return _mappings.TryGetValue(key, out mapper);
    }

    internal IReadOnlyDictionary<(Type Source, Type Target), Func<object, MappingContext?, object>> GetMappings()
    {
        return new ReadOnlyDictionary<(Type Source, Type Target), Func<object, MappingContext?, object>>(_mappings);
    }
}
