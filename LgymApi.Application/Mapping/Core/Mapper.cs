using System.Collections.ObjectModel;

namespace LgymApi.Application.Mapping.Core;

public sealed class Mapper : IMapper
{
    private readonly IReadOnlyDictionary<(Type Source, Type Target), Func<object, MappingContext?, object>> _mappings;
    private readonly IReadOnlySet<string> _allowedContextKeys;

    internal Mapper(IEnumerable<IMappingProfile> profiles)
    {
        var configuration = new MappingConfiguration();
        foreach (var profile in profiles)
        {
            profile.Configure(configuration);
        }

        _mappings = new ReadOnlyDictionary<(Type Source, Type Target), Func<object, MappingContext?, object>>(configuration.GetMappings().ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        _allowedContextKeys = new HashSet<string>(configuration.AllowedContextKeys, StringComparer.Ordinal);
        ValidateMappings();
    }

    public TTarget Map<TSource, TTarget>(TSource source, MappingContext? context = null)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        var key = (Source: typeof(TSource), Target: typeof(TTarget));
        if (!_mappings.TryGetValue(key, out var mapper))
        {
            throw new InvalidOperationException($"Mapping from {key.Source.Name} to {key.Target.Name} is not registered.");
        }

        return (TTarget)mapper(source!, context);
    }

    public List<TTarget> MapList<TSource, TTarget>(IEnumerable<TSource> source, MappingContext? context = null)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        var key = (Source: typeof(TSource), Target: typeof(TTarget));
        if (!_mappings.TryGetValue(key, out var mapper))
        {
            throw new InvalidOperationException($"Mapping from {key.Source.Name} to {key.Target.Name} is not registered.");
        }

        var result = new List<TTarget>();
        foreach (var item in source)
        {
            if (item is null)
            {
                continue;
            }

            result.Add((TTarget)mapper(item!, context));
        }

        return result;
    }

    internal IReadOnlyCollection<(Type Source, Type Target)> RegisteredMappings => _mappings.Keys.ToList();

    internal void ValidateMappings()
    {
        foreach (var (key, mapper) in _mappings)
        {
            object? instance = null;

            if (key.Source.IsValueType)
            {
                instance = Activator.CreateInstance(key.Source);
            }
            else
            {
                var ctor = key.Source.GetConstructor(Type.EmptyTypes);
                if (ctor != null)
                {
                    instance = Activator.CreateInstance(key.Source);
                }
            }

            if (instance is null)
            {
                continue;
            }

            mapper(instance, new MappingContext(_allowedContextKeys));
        }
    }

    public MappingContext CreateContext()
    {
        return new MappingContext(_allowedContextKeys);
    }
}
