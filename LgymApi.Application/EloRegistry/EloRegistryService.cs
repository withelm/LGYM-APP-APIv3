using System.Globalization;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.EloRegistry.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;

namespace LgymApi.Application.Features.EloRegistry;

public sealed class EloRegistryService : IEloRegistryService
{
    private readonly IUserRepository _userRepository;
    private readonly IEloRegistryRepository _eloRepository;

    public EloRegistryService(IUserRepository userRepository, IEloRegistryRepository eloRepository)
    {
        _userRepository = userRepository;
        _eloRepository = eloRepository;
    }

    public async Task<Result<List<EloRegistryChartEntry>, AppError>> GetChartAsync(Id<LgymApi.Domain.Entities.User> userId, CancellationToken cancellationToken = default)
    {
        if (userId.IsEmpty)
        {
            return Result<List<EloRegistryChartEntry>, AppError>.Failure(new EloRegistryNotFoundError(Messages.DidntFind));
        }

        var user = await _userRepository.FindByIdAsync((Id<LgymApi.Domain.Entities.User>)userId, cancellationToken);
        if (user == null)
        {
            return Result<List<EloRegistryChartEntry>, AppError>.Failure(new EloRegistryNotFoundError(Messages.DidntFind));
        }

        var eloRegistry = await _eloRepository.GetByUserIdAsync(user.Id, cancellationToken);
        if (eloRegistry.Count == 0)
        {
            return Result<List<EloRegistryChartEntry>, AppError>.Failure(new EloRegistryNotFoundError(Messages.DidntFind));
        }

        var result = eloRegistry.Select(entry => new EloRegistryChartEntry
        {
            Id = entry.Id.ToString(),
            Value = entry.Elo,
            Date = entry.Date.UtcDateTime.ToString("MM/dd", CultureInfo.InvariantCulture)
        }).ToList();

        return Result<List<EloRegistryChartEntry>, AppError>.Success(result);
    }
}
