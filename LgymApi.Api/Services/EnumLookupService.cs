using System.Globalization;
using System.Reflection;
using System.Resources;
using LgymApi.Api.DTOs;
using LgymApi.Domain.Enums;

namespace LgymApi.Api.Services;

public interface IEnumLookupService
{
    List<EnumLookupDto> GetLookup<TEnum>(CultureInfo? culture = null) where TEnum : struct, Enum;
    EnumLookupResponseDto? GetLookupByName(string enumTypeName, CultureInfo? culture = null);
    List<string> GetAvailableEnumTypes();
}

public sealed class EnumLookupService : IEnumLookupService
{
    private static readonly ResourceManager EnumResourceManager =
        new("LgymApi.Resources.Resources.Enums", typeof(LgymApi.Resources.Messages).Assembly);

    private static readonly Dictionary<string, Type> EnumTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { nameof(BodyParts), typeof(BodyParts) },
        { nameof(WeightUnits), typeof(WeightUnits) },
        { nameof(HeightUnits), typeof(HeightUnits) },
        { nameof(Platforms), typeof(Platforms) }
    };

    public List<EnumLookupDto> GetLookup<TEnum>(CultureInfo? culture = null) where TEnum : struct, Enum
    {
        culture ??= CultureInfo.CurrentUICulture;

        return Enum.GetValues<TEnum>()
            .Cast<Enum>()
            .OrderBy(e => Convert.ToInt32(e))
            .Select(e => e.ToLookup(culture))
            .ToList();
    }

    public EnumLookupResponseDto? GetLookupByName(string enumTypeName, CultureInfo? culture = null)
    {
        if (!EnumTypes.TryGetValue(enumTypeName, out var enumType))
        {
            return null;
        }

        culture ??= CultureInfo.CurrentUICulture;

        var values = new List<EnumLookupDto>();
        foreach (Enum enumValue in Enum.GetValues(enumType))
        {
            values.Add(enumValue.ToLookup(culture));
        }

        return new EnumLookupResponseDto
        {
            EnumType = enumType.Name,
            Values = values
        };
    }

    public List<string> GetAvailableEnumTypes()
    {
        return EnumTypes.Keys.OrderBy(k => k).ToList();
    }

    internal static EnumLookupDto ToLookupDtoInternal(Enum enumValue, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentUICulture;
        var enumName = enumValue.ToString();

        var translationKey = enumValue.GetType()
            .GetField(enumName)?
            .GetCustomAttribute<EnumTranslationAttribute>()?.ResourceKey
            ?? $"{enumValue.GetType().Name}_{enumName}";

        var displayName = EnumResourceManager.GetString(translationKey, culture) ?? enumName;

        return new EnumLookupDto
        {
            Name = enumName,
            DisplayName = displayName
        };
    }
}

public static class EnumLookupExtensions
{
    public static EnumLookupDto ToLookup(this Enum enumValue, CultureInfo? culture = null)
    {
        return EnumLookupService.ToLookupDtoInternal(enumValue, culture);
    }
}
