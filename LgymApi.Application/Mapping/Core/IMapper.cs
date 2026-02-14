namespace LgymApi.Application.Mapping.Core;

public interface IMapper
{
    TTarget Map<TSource, TTarget>(TSource source, MappingContext? context = null);
    List<TTarget> MapList<TSource, TTarget>(IEnumerable<TSource> source, MappingContext? context = null);

    MappingContext CreateContext();
}
