namespace LgymApi.Application.Mapping.Core;

public interface IMappingContext
{
    T? Get<T>(ContextKey<T> key);
    void Set<T>(ContextKey<T> key, T value);
}
