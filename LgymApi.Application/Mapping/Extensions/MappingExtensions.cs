using LgymApi.Application.Mapping.Core;

namespace LgymApi.Application.Mapping.Extensions;

public static class MappingExtensions
{
    public static TTarget MapTo<TSource, TTarget>(this TSource source, IMapper mapper, MappingContext? context = null)
    {
        return mapper.Map<TSource, TTarget>(source, context);
    }

    public static List<TTarget> MapToList<TSource, TTarget>(this IEnumerable<TSource> source, IMapper mapper, MappingContext? context = null)
    {
        return mapper.MapList<TSource, TTarget>(source, context);
    }
}
