using LgymApi.Domain.Entities;

namespace LgymApi.Application.Repositories;

public interface IMeasurementRepository
{
    Task AddAsync(Measurement measurement, CancellationToken cancellationToken = default);
    Task<Measurement?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Measurement>> GetByUserAsync(Guid userId, string? bodyPart, CancellationToken cancellationToken = default);
}
