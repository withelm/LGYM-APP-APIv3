using LgymApi.Application.Exceptions;
using LgymApi.Domain.Enums;
using LgymApi.Resources;

namespace LgymApi.Application.Features.Training.Elo;

public sealed class EloCalculationStrategyResolver : IEloCalculationStrategyResolver
{
    private readonly IReadOnlyDictionary<EloStrategy, IEloCalculationStrategy> _strategies;

    public EloCalculationStrategyResolver(IEnumerable<IEloCalculationStrategy> strategies)
    {
        _strategies = strategies.ToDictionary(x => x.Strategy, x => x);
    }

    public IEloCalculationStrategy Resolve(EloStrategy strategy)
    {
        if (_strategies.TryGetValue(strategy, out var resolved))
        {
            return resolved;
        }

        throw AppException.BadRequest(Messages.FieldRequired);
    }
}
