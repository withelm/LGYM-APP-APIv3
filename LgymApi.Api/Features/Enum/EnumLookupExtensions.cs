using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Resources;
using LgymApi.Api.Features.Enum.Contracts;
using LgymApi.Domain.Enums;

namespace LgymApi.Api.Features.Enum;

public static class EnumLookupExtensions
{
    private static readonly ConcurrentDictionary<(Type EnumType, string EnumName), string> TranslationKeyCache = new();
    private static readonly ResourceManager EnumResourceManager =
        new("LgymApi.Resources.Resources.Enums", typeof(LgymApi.Resources.Enums).Assembly);

    public static EnumLookupDto ToLookup(this System.Enum enumValue, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentUICulture;
        var enumName = enumValue.ToString();

        var enumType = enumValue.GetType();
        var translationKey = TranslationKeyCache.GetOrAdd((enumType, enumName), key =>
        {
            var field = key.EnumType.GetField(key.EnumName);
            var attributeKey = field?.GetCustomAttribute<EnumTranslationAttribute>()?.ResourceKey;
            return attributeKey ?? $"{key.EnumType.Name}_{key.EnumName}";
        });

        var displayName = EnumResourceManager.GetString(translationKey, culture) ?? enumName;

        return new EnumLookupDto
        {
            Name = enumName,
            DisplayName = displayName
        };
    }
}
