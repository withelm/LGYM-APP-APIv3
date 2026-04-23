using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
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
            .ThenInclude(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .ThenInclude(r => r.RoleClaims)
            .FirstOrDefaultAsync(x => x.Provider == provider && x.ProviderKey == providerKey && !x.IsDeleted, cancellationToken);
    }

    public Task<List<UserExternalLogin>> GetByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.UserExternalLogins
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsDeleted)
            .OrderBy(x => x.Provider)
            .ToListAsync(cancellationToken);
    }
}
