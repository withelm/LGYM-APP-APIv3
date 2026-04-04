using System.Globalization;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.Enum.Models;

namespace LgymApi.Application.Features.Enum;

public interface IEnumService
{
    List<EnumLookupEntry> GetLookup<TEnum>(CultureInfo? culture = null) where TEnum : struct, System.Enum;
    EnumLookupResponse? GetLookupByName(string enumTypeName, CultureInfo? culture = null);
    Task<Result<EnumLookupResponse, AppError>> GetLookupByNameAsync(string enumTypeName, CultureInfo? culture = null, CancellationToken ct = default);
    List<string> GetAvailableEnumTypes();
    EnumLookupEntry ToLookup(System.Enum enumValue, CultureInfo? culture = null);
}
