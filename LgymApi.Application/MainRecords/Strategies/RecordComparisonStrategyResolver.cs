using LgymApi.Application.Exceptions;
using LgymApi.Domain.Enums;
using LgymApi.Resources;

namespace LgymApi.Application.Features.MainRecords.Strategies;

public sealed class RecordComparisonStrategyResolver : IRecordComparisonStrategyResolver
{
    private readonly IReadOnlyDictionary<EloStrategy, IRecordComparisonStrategy> _strategies;

    public RecordComparisonStrategyResolver(IEnumerable<IRecordComparisonStrategy> strategies)
    {
        _strategies = strategies.ToDictionary(x => x.Strategy, x => x);
    }

    public IRecordComparisonStrategy Resolve(EloStrategy strategy)
    {
        if (_strategies.TryGetValue(strategy, out var resolved))
        {
            return resolved;
        }

        throw AppException.BadRequest(Messages.FieldRequired);
    }
}
