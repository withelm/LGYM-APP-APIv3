using LgymApi.Application.Repositories;
using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class TrainerRelationshipRepository : ITrainerRelationshipRepository
{
    private readonly AppDbContext _dbContext;

    public TrainerRelationshipRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddInvitationAsync(TrainerInvitation invitation, CancellationToken cancellationToken = default)
    {
        await _dbContext.TrainerInvitations.AddAsync(invitation, cancellationToken);
    }

    public Task<TrainerInvitation?> FindInvitationByIdAsync(Guid invitationId, CancellationToken cancellationToken = default)
    {
        return _dbContext.TrainerInvitations
            .FirstOrDefaultAsync(i => i.Id == invitationId, cancellationToken);
    }

    public Task<TrainerInvitation?> FindPendingInvitationAsync(Guid trainerId, Guid traineeId, CancellationToken cancellationToken = default)
    {
        return _dbContext.TrainerInvitations
            .Where(i => i.TrainerId == trainerId && i.TraineeId == traineeId && i.Status == TrainerInvitationStatus.Pending)
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<List<TrainerInvitation>> GetInvitationsByTrainerIdAsync(Guid trainerId, CancellationToken cancellationToken = default)
    {
        return _dbContext.TrainerInvitations
            .Where(i => i.TrainerId == trainerId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasActiveLinkForTraineeAsync(Guid traineeId, CancellationToken cancellationToken = default)
    {
        return _dbContext.TrainerTraineeLinks.AnyAsync(l => l.TraineeId == traineeId, cancellationToken);
    }

    public Task<TrainerTraineeLink?> FindActiveLinkByTrainerAndTraineeAsync(Guid trainerId, Guid traineeId, CancellationToken cancellationToken = default)
    {
        return _dbContext.TrainerTraineeLinks
            .FirstOrDefaultAsync(l => l.TrainerId == trainerId && l.TraineeId == traineeId, cancellationToken);
    }

    public Task<TrainerTraineeLink?> FindActiveLinkByTraineeIdAsync(Guid traineeId, CancellationToken cancellationToken = default)
    {
        return _dbContext.TrainerTraineeLinks
            .FirstOrDefaultAsync(l => l.TraineeId == traineeId, cancellationToken);
    }

    public async Task<TrainerDashboardTraineeListResult> GetDashboardTraineesAsync(Guid trainerId, TrainerDashboardTraineeQuery query, CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);
        var now = DateTimeOffset.UtcNow;

        var trainerLinks = _dbContext.TrainerTraineeLinks
            .AsNoTracking()
            .Where(x => x.TrainerId == trainerId);

        var trainerInvitations = _dbContext.TrainerInvitations
            .AsNoTracking()
            .Where(x => x.TrainerId == trainerId);

        var ownedTraineeIds = trainerLinks
            .Select(x => x.TraineeId)
            .Union(trainerInvitations.Select(x => x.TraineeId))
            .Distinct();

        var baseQuery =
            from user in _dbContext.Users.AsNoTracking()
            where !user.IsDeleted && ownedTraineeIds.Contains(user.Id)
            select new DashboardTraineeProjection
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Avatar = user.Avatar,
                CreatedAt = user.CreatedAt,
                LinkedAt = trainerLinks
                    .Where(l => l.TraineeId == user.Id)
                    .Select(l => (DateTimeOffset?)l.CreatedAt)
                    .FirstOrDefault(),
                LastInvitationStatus = trainerInvitations
                    .Where(i => i.TraineeId == user.Id)
                    .OrderByDescending(i => i.CreatedAt)
                    .ThenByDescending(i => i.Id)
                    .Select(i => (TrainerInvitationStatus?)i.Status)
                    .FirstOrDefault(),
                LastInvitationExpiresAt = trainerInvitations
                    .Where(i => i.TraineeId == user.Id)
                    .OrderByDescending(i => i.CreatedAt)
                    .ThenByDescending(i => i.Id)
                    .Select(i => (DateTimeOffset?)i.ExpiresAt)
                    .FirstOrDefault(),
                LastInvitationRespondedAt = trainerInvitations
                    .Where(i => i.TraineeId == user.Id)
                    .OrderByDescending(i => i.CreatedAt)
                    .ThenByDescending(i => i.Id)
                    .Select(i => i.RespondedAt)
                    .FirstOrDefault(),
                IsLinked = trainerLinks.Any(l => l.TraineeId == user.Id)
            };

        baseQuery = baseQuery
            .Select(x => new DashboardTraineeProjection
            {
                Id = x.Id,
                Name = x.Name,
                Email = x.Email,
                Avatar = x.Avatar,
                CreatedAt = x.CreatedAt,
                LinkedAt = x.LinkedAt,
                LastInvitationStatus = x.LastInvitationStatus,
                LastInvitationExpiresAt = x.LastInvitationExpiresAt,
                LastInvitationRespondedAt = x.LastInvitationRespondedAt,
                IsLinked = x.IsLinked,
                HasPendingInvitation = !x.IsLinked
                    && x.LastInvitationStatus == TrainerInvitationStatus.Pending
                    && x.LastInvitationExpiresAt > now,
                HasExpiredInvitation = !x.IsLinked
                    && (x.LastInvitationStatus == TrainerInvitationStatus.Expired
                        || (x.LastInvitationStatus == TrainerInvitationStatus.Pending && x.LastInvitationExpiresAt <= now)),
                Status = x.IsLinked
                    ? TrainerDashboardTraineeStatus.Linked
                    : x.LastInvitationStatus == null
                        ? TrainerDashboardTraineeStatus.NoRelationship
                        : x.LastInvitationStatus == TrainerInvitationStatus.Accepted
                            ? TrainerDashboardTraineeStatus.InvitationAccepted
                            : x.LastInvitationStatus == TrainerInvitationStatus.Rejected
                                ? TrainerDashboardTraineeStatus.InvitationRejected
                                : x.LastInvitationStatus == TrainerInvitationStatus.Expired
                                    || (x.LastInvitationStatus == TrainerInvitationStatus.Pending && x.LastInvitationExpiresAt <= now)
                                        ? TrainerDashboardTraineeStatus.InvitationExpired
                                        : x.LastInvitationStatus == TrainerInvitationStatus.Pending
                                            ? TrainerDashboardTraineeStatus.InvitationPending
                                            : TrainerDashboardTraineeStatus.NoRelationship
            });

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            baseQuery = baseQuery.Where(x => x.Name.ToLower().Contains(search) || x.Email.ToLower().Contains(search));
        }

        if (Enum.TryParse<TrainerDashboardTraineeStatus>(query.Status, true, out var statusFilter))
        {
            baseQuery = baseQuery.Where(x => x.Status == statusFilter);
        }

        var sortBy = query.SortBy?.Trim().ToLowerInvariant();
        var sortDescending = string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        baseQuery = (sortBy, sortDescending) switch
        {
            ("createdat", true) => baseQuery.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Name),
            ("createdat", false) => baseQuery.OrderBy(x => x.CreatedAt).ThenBy(x => x.Name),
            ("status", true) => baseQuery.OrderByDescending(x => x.Status).ThenBy(x => x.Name),
            ("status", false) => baseQuery.OrderBy(x => x.Status).ThenBy(x => x.Name),
            (_, true) => baseQuery.OrderByDescending(x => x.Name),
            _ => baseQuery.OrderBy(x => x.Name)
        };

        var total = await baseQuery.CountAsync(cancellationToken);
        var items = await baseQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new TrainerDashboardTraineeResult
            {
                Id = x.Id,
                Name = x.Name,
                Email = x.Email,
                Avatar = x.Avatar,
                Status = x.Status,
                IsLinked = x.IsLinked,
                HasPendingInvitation = x.HasPendingInvitation,
                HasExpiredInvitation = x.HasExpiredInvitation,
                LinkedAt = x.LinkedAt,
                LastInvitationExpiresAt = x.LastInvitationExpiresAt,
                LastInvitationRespondedAt = x.LastInvitationRespondedAt
            })
            .ToListAsync(cancellationToken);

        return new TrainerDashboardTraineeListResult
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            Items = items
        };
    }

    public async Task AddLinkAsync(TrainerTraineeLink link, CancellationToken cancellationToken = default)
    {
        await _dbContext.TrainerTraineeLinks.AddAsync(link, cancellationToken);
    }

    public Task RemoveLinkAsync(TrainerTraineeLink link, CancellationToken cancellationToken = default)
    {
        _dbContext.TrainerTraineeLinks.Remove(link);
        return Task.CompletedTask;
    }

    private sealed class DashboardTraineeProjection
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string? Avatar { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? LinkedAt { get; init; }
        public TrainerInvitationStatus? LastInvitationStatus { get; init; }
        public DateTimeOffset? LastInvitationExpiresAt { get; init; }
        public DateTimeOffset? LastInvitationRespondedAt { get; init; }
        public TrainerDashboardTraineeStatus Status { get; init; }
        public bool IsLinked { get; init; }
        public bool HasPendingInvitation { get; init; }
        public bool HasExpiredInvitation { get; init; }
    }
}
