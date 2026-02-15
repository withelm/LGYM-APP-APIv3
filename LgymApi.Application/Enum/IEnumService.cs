using System.Globalization;
using LgymApi.Application.Features.Enum.Models;

namespace LgymApi.Application.Features.Enum;

public interface IEnumService
{
    List<EnumLookupEntry> GetLookup<TEnum>(CultureInfo? culture = null) where TEnum : struct, System.Enum;
    EnumLookupResponse? GetLookupByName(string enumTypeName, CultureInfo? culture = null);
    List<string> GetAvailableEnumTypes();
    EnumLookupEntry ToLookup(System.Enum enumValue, CultureInfo? culture = null);
}
