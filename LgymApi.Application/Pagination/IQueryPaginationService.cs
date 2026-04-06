using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.Pagination;

public interface IQueryPaginationService    
{
    Task<Result<Pagination<TProjection>, AppError>> ExecuteAsync<TProjection>(
        Func<IQueryable<TProjection>> queryFactory,
        FilterInput filterInput,
        CancellationToken cancellationToken = default)
        where TProjection : class;
}
