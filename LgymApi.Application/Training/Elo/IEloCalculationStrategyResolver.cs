using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.Training.Elo;

public interface IEloCalculationStrategyResolver
{
    IEloCalculationStrategy Resolve(EloStrategy strategy);
}
