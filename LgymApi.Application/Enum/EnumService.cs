using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Resources;
using LgymApi.Application.Features.Enum.Models;
using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.Enum;

public sealed class EnumService : IEnumService
{
    private static readonly ConcurrentDictionary<(Type EnumType, string EnumName), string> TranslationKeyCache = new();
    private static readonly ConcurrentDictionary<(Type EnumType, string EnumName), bool> HiddenValueCache = new();
    private static readonly ResourceManager EnumResourceManager =
        new("LgymApi.Resources.Resources.Enums", typeof(LgymApi.Resources.Enums).Assembly);

    private static readonly Dictionary<string, Type> EnumTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { nameof(BodyParts), typeof(BodyParts) },
        { nameof(WeightUnits), typeof(WeightUnits) },
        { nameof(HeightUnits), typeof(HeightUnits) },
        { nameof(Platforms), typeof(Platforms) }
    };

    public List<EnumLookupEntry> GetLookup<TEnum>(CultureInfo? culture = null) where TEnum : struct, System.Enum
    {
        culture ??= CultureInfo.CurrentUICulture;

        return System.Enum.GetValues<TEnum>()
            .Cast<System.Enum>()
            .Where(e => !IsHidden(e))
            .Select(e => ToLookup(e, culture))
            .ToList();
    }

    public EnumLookupResponse? GetLookupByName(string enumTypeName, CultureInfo? culture = null)
    {
        if (!EnumTypes.TryGetValue(enumTypeName, out var enumType))
        {
            return null;
        }

        culture ??= CultureInfo.CurrentUICulture;

        var values = new List<EnumLookupEntry>();
        foreach (System.Enum enumValue in System.Enum.GetValues(enumType))
        {
            if (IsHidden(enumValue))
            {
                continue;
            }

            values.Add(ToLookup(enumValue, culture));
        }

        return new EnumLookupResponse
        {
            EnumType = enumType.Name,
            Values = values
        };
    }

    public List<string> GetAvailableEnumTypes()
    {
        return EnumTypes.Keys.OrderBy(k => k).ToList();
    }

    public EnumLookupEntry ToLookup(System.Enum enumValue, CultureInfo? culture = null)
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

        return new EnumLookupEntry
        {
            Name = enumName,
            DisplayName = displayName
        };
    }

    private static bool IsHidden(System.Enum enumValue)
    {
        var enumType = enumValue.GetType();
        var enumName = enumValue.ToString();

        return HiddenValueCache.GetOrAdd((enumType, enumName), key =>
        {
            var field = key.EnumType.GetField(key.EnumName);
            return field?.GetCustomAttribute<HiddenAttribute>() != null;
        });
    }
}
