namespace LgymApi.Infrastructure.Reporting.PhotoTypesMigration;

internal static class MigrationDictionaryExtensions
{
    public static TValue GetOrAdd<TKey, TValue>(
        this IDictionary<TKey, TValue> dictionary,
        TKey key,
        Func<TValue> factory)
    {
        if (dictionary.TryGetValue(key, out var value))
        {
            return value;
        }
        value = factory();
        dictionary.Add(key, value);
        return value;
    }
}
