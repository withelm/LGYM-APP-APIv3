using System.Globalization;
using LgymApi.Application.Features.EloRegistry.Models;
using LgymApi.Application.Exceptions;
using LgymApi.Application.Repositories;
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

    public async Task<List<EloRegistryChartEntry>> GetChartAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var user = await _userRepository.FindByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var eloRegistry = await _eloRepository.GetByUserIdAsync(user.Id, cancellationToken);
        if (eloRegistry.Count == 0)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return eloRegistry.Select(entry => new EloRegistryChartEntry
        {
            Id = entry.Id.ToString(),
            Value = entry.Elo,
            Date = entry.Date.UtcDateTime.ToString("MM/dd", CultureInfo.InvariantCulture)
        }).ToList();
    }
}
