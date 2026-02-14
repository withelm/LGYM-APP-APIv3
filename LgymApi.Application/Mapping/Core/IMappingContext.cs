namespace LgymApi.Application.Mapping.Core;

public interface IMappingContext
{
    T? Get<T>(string key);
    void Set<T>(string key, T value);
}
