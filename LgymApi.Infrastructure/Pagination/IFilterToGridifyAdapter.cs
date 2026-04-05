using LgymApi.Application.Pagination;

namespace LgymApi.Infrastructure.Pagination;

public interface IFilterToGridifyAdapter
{
    string Adapt(FilterInput input);
}
