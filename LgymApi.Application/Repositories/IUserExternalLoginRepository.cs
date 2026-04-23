using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Repositories;

public interface IUserExternalLoginRepository
{
    Task AddAsync(UserExternalLogin externalLogin, CancellationToken cancellationToken = default);
    Task<UserExternalLogin?> FindByProviderAsync(string provider, string providerKey, CancellationToken cancellationToken = default);
    Task<List<UserExternalLogin>> GetByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default);
}
