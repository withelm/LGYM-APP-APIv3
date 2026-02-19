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
                    .FirstOrDefault()
            };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            var pattern = $"%{search}%";
            var isNpgsql = _dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
            baseQuery = isNpgsql
                ? baseQuery.Where(x => EF.Functions.ILike(x.Name, pattern) || EF.Functions.ILike(x.Email, pattern))
                : baseQuery.Where(x => EF.Functions.Like(x.Name, pattern) || EF.Functions.Like(x.Email, pattern));
        }

        if (Enum.TryParse<TrainerDashboardTraineeStatus>(query.Status, true, out var statusFilter))
        {
            baseQuery = statusFilter switch
            {
                TrainerDashboardTraineeStatus.Linked => baseQuery.Where(x => x.LinkedAt != null),
                TrainerDashboardTraineeStatus.InvitationPending => baseQuery.Where(x =>
                    x.LinkedAt == null
                    && x.LastInvitationStatus == TrainerInvitationStatus.Pending
                    && x.LastInvitationExpiresAt > now),
                TrainerDashboardTraineeStatus.InvitationExpired => baseQuery.Where(x =>
                    x.LinkedAt == null
                    && (x.LastInvitationStatus == TrainerInvitationStatus.Expired
                        || (x.LastInvitationStatus == TrainerInvitationStatus.Pending && x.LastInvitationExpiresAt <= now))),
                TrainerDashboardTraineeStatus.InvitationRejected => baseQuery.Where(x =>
                    x.LinkedAt == null
                    && x.LastInvitationStatus == TrainerInvitationStatus.Rejected),
                TrainerDashboardTraineeStatus.InvitationAccepted => baseQuery.Where(x =>
                    x.LinkedAt == null
                    && x.LastInvitationStatus == TrainerInvitationStatus.Accepted),
                TrainerDashboardTraineeStatus.NoRelationship => baseQuery.Where(x =>
                    x.LinkedAt == null
                    && x.LastInvitationStatus == null),
                _ => baseQuery
            };
        }

        var sortBy = query.SortBy?.Trim().ToLowerInvariant();
        var sortDescending = string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        baseQuery = (sortBy, sortDescending) switch
        {
            ("createdat", true) => baseQuery.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Name),
            ("createdat", false) => baseQuery.OrderBy(x => x.CreatedAt).ThenBy(x => x.Name),
            ("status", true) => baseQuery
                .OrderByDescending(x =>
                    x.LinkedAt != null ? 0 :
                    (x.LinkedAt == null
                        && x.LastInvitationStatus == TrainerInvitationStatus.Pending
                        && x.LastInvitationExpiresAt > now) ? 1 :
                    (x.LinkedAt == null
                        && (x.LastInvitationStatus == TrainerInvitationStatus.Expired
                            || (x.LastInvitationStatus == TrainerInvitationStatus.Pending
                                && x.LastInvitationExpiresAt <= now))) ? 2 :
                    (x.LinkedAt == null
                        && x.LastInvitationStatus == TrainerInvitationStatus.Rejected) ? 3 :
                    (x.LinkedAt == null
                        && x.LastInvitationStatus == TrainerInvitationStatus.Accepted) ? 4 :
                    (x.LinkedAt == null
                        && x.LastInvitationStatus == null) ? 5 :
                    6)
                .ThenBy(x => x.Name),
            ("status", false) => baseQuery
                .OrderBy(x =>
                    x.LinkedAt != null ? 0 :
                    (x.LinkedAt == null
                        && x.LastInvitationStatus == TrainerInvitationStatus.Pending
                        && x.LastInvitationExpiresAt > now) ? 1 :
                    (x.LinkedAt == null
                        && (x.LastInvitationStatus == TrainerInvitationStatus.Expired
                            || (x.LastInvitationStatus == TrainerInvitationStatus.Pending
                                && x.LastInvitationExpiresAt <= now))) ? 2 :
                    (x.LinkedAt == null
                        && x.LastInvitationStatus == TrainerInvitationStatus.Rejected) ? 3 :
                    (x.LinkedAt == null
                        && x.LastInvitationStatus == TrainerInvitationStatus.Accepted) ? 4 :
                    (x.LinkedAt == null
                        && x.LastInvitationStatus == null) ? 5 :
                    6)
                .ThenBy(x => x.Name),
            (_, true) => baseQuery.OrderByDescending(x => x.Name),
            _ => baseQuery.OrderBy(x => x.Name)
        };

        var offsetLong = ((long)page - 1L) * pageSize;
        if (offsetLong < 0)
        {
            offsetLong = 0;
        }
        else if (offsetLong > int.MaxValue)
        {
            offsetLong = int.MaxValue;
        }

        var offset = (int)offsetLong;

        var total = await baseQuery.CountAsync(cancellationToken);
        var items = await baseQuery
            .Skip(offset)
            .Take(pageSize)
            .Select(x => new TrainerDashboardTraineeResult
            {
                Id = x.Id,
                Name = x.Name,
                Email = x.Email,
                Avatar = x.Avatar,
                Status = x.LinkedAt != null
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
                                            : TrainerDashboardTraineeStatus.NoRelationship,
                IsLinked = x.LinkedAt != null,
                HasPendingInvitation = x.LinkedAt == null
                    && x.LastInvitationStatus == TrainerInvitationStatus.Pending
                    && x.LastInvitationExpiresAt > now,
                HasExpiredInvitation = x.LinkedAt == null
                    && (x.LastInvitationStatus == TrainerInvitationStatus.Expired
                        || (x.LastInvitationStatus == TrainerInvitationStatus.Pending && x.LastInvitationExpiresAt <= now)),
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
    }
}
