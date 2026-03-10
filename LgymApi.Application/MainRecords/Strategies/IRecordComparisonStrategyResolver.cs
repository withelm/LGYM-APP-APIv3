using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.MainRecords.Strategies;

public interface IRecordComparisonStrategyResolver
{
    IRecordComparisonStrategy Resolve(EloStrategy strategy);
}
