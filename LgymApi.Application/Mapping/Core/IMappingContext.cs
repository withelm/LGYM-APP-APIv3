namespace LgymApi.Application.Mapping.Core;

public interface IMappingContext
{
    T? Get<T>(ContextKey<T> key);
    void Set<T>(ContextKey<T> key, T value);
    TTarget Map<TSource, TTarget>(TSource source);
    List<TTarget> MapList<TSource, TTarget>(IEnumerable<TSource> source);
}
