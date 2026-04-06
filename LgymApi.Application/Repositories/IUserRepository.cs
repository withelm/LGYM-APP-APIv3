using LgymApi.Application.Models;
using LgymApi.Application.Pagination;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Repositories;

public interface IUserRepository
{
    Task<User?> FindByIdAsync(Id<User> id, CancellationToken cancellationToken = default);
    Task<User?> FindByIdIncludingDeletedAsync(Id<User> id, CancellationToken cancellationToken = default);
    Task<User?> FindByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default);
    Task<User?> FindByNameOrEmailAsync(string name, string email, CancellationToken cancellationToken = default);
    Task<List<UserRankingEntry>> GetRankingAsync(CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
    Task<Pagination<AdminUserListItem>> GetUsersPaginatedAsync(FilterInput filterInput, bool includeDeleted, CancellationToken cancellationToken = default);
}

public sealed class AdminUserListItem
{
    public Id<User> Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Avatar { get; init; }
    public string ProfileRank { get; init; } = string.Empty;
    public bool IsVisibleInRanking { get; init; }
    public bool IsBlocked { get; init; }
    public bool IsDeleted { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
