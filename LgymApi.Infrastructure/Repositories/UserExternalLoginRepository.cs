using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class UserExternalLoginRepository : IUserExternalLoginRepository
{
    private readonly AppDbContext _dbContext;

    public UserExternalLoginRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(UserExternalLogin externalLogin, CancellationToken cancellationToken = default)
    {
        return _dbContext.UserExternalLogins.AddAsync(externalLogin, cancellationToken).AsTask();
    }

    public Task<UserExternalLogin?> FindByProviderAsync(string provider, string providerKey, CancellationToken cancellationToken = default)
    {
        return _dbContext.UserExternalLogins
            .AsNoTracking()
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Provider == provider && x.ProviderKey == providerKey, cancellationToken);
    }

    public Task<UserExternalLogin?> FindByUserAndProviderAsync(Id<User> userId, string provider, CancellationToken cancellationToken = default)
    {
        return _dbContext.UserExternalLogins
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider == provider, cancellationToken);
    }

    public Task<UserExternalLogin?> FindActiveGoogleByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.UserExternalLogins
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider == AuthConstants.ExternalProviders.Google, cancellationToken);
    }

    public async Task MarkGoogleDeletedAsync(Id<User> userId, CancellationToken cancellationToken = default)
    {
        await _dbContext.UserExternalLogins
            .Where(x => x.UserId == userId && x.Provider == AuthConstants.ExternalProviders.Google)
            .StageUpdateAsync(_dbContext, x => x.IsDeleted, _ => true, cancellationToken);
    }

    public Task<List<UserExternalLogin>> GetByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.UserExternalLogins
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Provider)
            .ToListAsync(cancellationToken);
    }
}
