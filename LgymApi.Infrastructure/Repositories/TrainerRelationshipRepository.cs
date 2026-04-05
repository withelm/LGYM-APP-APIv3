using LgymApi.Application.Pagination;
using LgymApi.Application.Repositories;
using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Pagination;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class TrainerRelationshipRepository : ITrainerRelationshipRepository
{
    private readonly AppDbContext _dbContext;
    private readonly GridifyExecutionService _gridifyExecutionService;
    private readonly IMapperRegistry _mapperRegistry;

    private static readonly PaginationPolicy DashboardPaginationPolicy = new()
    {
        MaxPageSize = 100,
        DefaultPageSize = 20,
        DefaultSortField = "name",
        TieBreakerField = "id"
    };

    public TrainerRelationshipRepository(
        AppDbContext dbContext,
        GridifyExecutionService gridifyExecutionService,
        IMapperRegistry mapperRegistry)
    {
        _dbContext = dbContext;
        _gridifyExecutionService = gridifyExecutionService;
        _mapperRegistry = mapperRegistry;
    }

    public async Task AddInvitationAsync(TrainerInvitation invitation, CancellationToken cancellationToken = default)
    {
        await _dbContext.TrainerInvitations.AddAsync(invitation, cancellationToken);
    }

    public Task<TrainerInvitation?> FindInvitationByIdAsync(Id<TrainerInvitation> invitationId, CancellationToken cancellationToken = default)
    {
        return _dbContext.TrainerInvitations
            .FirstOrDefaultAsync(i => i.Id == invitationId, cancellationToken);
    }

    public Task<TrainerInvitation?> FindPendingInvitationAsync(Id<User> trainerId, Id<User> traineeId, CancellationToken cancellationToken = default)
    {
        return _dbContext.TrainerInvitations
            .Where(i => i.TrainerId == trainerId
                && i.TraineeId == traineeId
                && i.Status == TrainerInvitationStatus.Pending)
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<TrainerInvitation?> FindPendingInvitationByEmailAsync(Id<User> trainerId, string inviteeEmail, CancellationToken cancellationToken = default)
    {
        return _dbContext.TrainerInvitations
            .AsNoTracking()
            .Where(i => i.TrainerId == trainerId
                && i.InviteeEmail == inviteeEmail
                && i.Status == TrainerInvitationStatus.Pending)
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> IsEmailAlreadyTraineeAsync(Id<User> trainerId, string inviteeEmail, CancellationToken cancellationToken = default)
    {
        return (
            from link in _dbContext.TrainerTraineeLinks.AsNoTracking()
            join user in _dbContext.Users.AsNoTracking() on link.TraineeId equals user.Id
            where link.TrainerId == trainerId && user.Email == inviteeEmail
            select link
        ).AnyAsync(cancellationToken);
    }

    public Task<TrainerInvitation?> FindInvitationByIdWithCodeAsync(Id<TrainerInvitation> invitationId, string code, CancellationToken cancellationToken = default)
    {
        return _dbContext.TrainerInvitations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == invitationId && i.Code == code, cancellationToken);
    }

    public Task<List<TrainerInvitation>> GetInvitationsByTrainerIdAsync(Id<User> trainerId, CancellationToken cancellationToken = default)
    {
        return _dbContext.TrainerInvitations
            .Where(i => i.TrainerId == trainerId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasActiveLinkForTraineeAsync(Id<User> traineeId, CancellationToken cancellationToken = default)
    {
        return _dbContext.TrainerTraineeLinks.AnyAsync(l => l.TraineeId == traineeId, cancellationToken);
    }

    public Task<TrainerTraineeLink?> FindActiveLinkByTrainerAndTraineeAsync(Id<User> trainerId, Id<User> traineeId, CancellationToken cancellationToken = default)
    {
        return _dbContext.TrainerTraineeLinks
            .FirstOrDefaultAsync(
                l => l.TrainerId == trainerId && l.TraineeId == traineeId,
                cancellationToken);
    }

    public Task<TrainerTraineeLink?> FindActiveLinkByTraineeIdAsync(Id<User> traineeId, CancellationToken cancellationToken = default)
    {
        return _dbContext.TrainerTraineeLinks
            .FirstOrDefaultAsync(l => l.TraineeId == traineeId, cancellationToken);
    }

    public async Task<TrainerDashboardTraineeListResult> GetDashboardTraineesAsync(Id<User> trainerId, TrainerDashboardTraineeQuery query, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var baseQuery = BuildDashboardBaseQuery(trainerId, now);
        baseQuery = ApplySearch(baseQuery, query.Search);
        baseQuery = ApplyStatusFilter(baseQuery, query.Status, now);

        var filterInput = BuildFilterInput(query);

        var paginationResult = await _gridifyExecutionService.ExecuteAsync(
            baseQuery,
            filterInput,
            _mapperRegistry,
            DashboardPaginationPolicy,
            cancellationToken);

        var items = paginationResult.Items.Select(x => new TrainerDashboardTraineeResult
            {
                Id = x.Id,
                Name = x.Name,
                Email = x.Email,
                Avatar = x.Avatar,
                Status = ResolveTraineeStatus(x, now),
                IsLinked = x.LinkedAt != null,
                HasPendingInvitation = HasPendingInvitation(x, now),
                HasExpiredInvitation = HasExpiredInvitation(x, now),
                LinkedAt = x.LinkedAt,
                LastInvitationExpiresAt = x.LastInvitationExpiresAt,
                LastInvitationRespondedAt = x.LastInvitationRespondedAt
            })
            .ToList();

        return new TrainerDashboardTraineeListResult
        {
            Page = paginationResult.Page,
            PageSize = paginationResult.PageSize,
            Total = paginationResult.TotalCount,
            Items = items
        };
    }

    private static FilterInput BuildFilterInput(TrainerDashboardTraineeQuery query)
    {
        var sortDescriptors = new List<SortDescriptor>();

        if (!string.IsNullOrWhiteSpace(query.SortBy))
        {
            var fieldName = query.SortBy.Trim().ToLowerInvariant() switch
            {
                "status" => "statusOrder",
                var other => other
            };

            sortDescriptors.Add(new SortDescriptor
            {
                FieldName = fieldName,
                Descending = string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase)
            });
        }

        return new FilterInput
        {
            Page = query.Page < 1 ? 1 : query.Page,
            PageSize = query.PageSize <= 0 ? 20 : query.PageSize,
            SortDescriptors = sortDescriptors
        };
    }

    private IQueryable<DashboardTraineeProjection> BuildDashboardBaseQuery(Id<User> trainerId, DateTimeOffset now)
    {

        var trainerLinks = _dbContext.TrainerTraineeLinks
            .AsNoTracking()
            .Where(x => x.TrainerId == trainerId);

        var trainerInvitations = _dbContext.TrainerInvitations
            .AsNoTracking()
            .Where(x => x.TrainerId == trainerId);

        var ownedTraineeIds = trainerLinks
            .Select(x => x.TraineeId)
            .Union(trainerInvitations
                .Where(x => x.TraineeId.HasValue)
                .Select(x => x.TraineeId!.Value))
            .Distinct();

        return
            from user in _dbContext.Users.AsNoTracking()
            where !user.IsDeleted && ownedTraineeIds.Contains(user.Id)
            let linkedAt = trainerLinks
                .Where(l => l.TraineeId == user.Id)
                .Select(l => (DateTimeOffset?)l.CreatedAt)
                .FirstOrDefault()
            let lastInvitationStatus = trainerInvitations
                .Where(i => i.TraineeId == user.Id)
                .OrderByDescending(i => i.CreatedAt)
                .ThenByDescending(i => i.Id)
                .Select(i => (TrainerInvitationStatus?)i.Status)
                .FirstOrDefault()
            let lastInvitationExpiresAt = trainerInvitations
                .Where(i => i.TraineeId == user.Id)
                .OrderByDescending(i => i.CreatedAt)
                .ThenByDescending(i => i.Id)
                .Select(i => (DateTimeOffset?)i.ExpiresAt)
                .FirstOrDefault()
            select new DashboardTraineeProjection
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Avatar = user.Avatar,
                CreatedAt = user.CreatedAt,
                LinkedAt = linkedAt,
                LastInvitationStatus = lastInvitationStatus,
                LastInvitationExpiresAt = lastInvitationExpiresAt,
                LastInvitationRespondedAt = trainerInvitations
                    .Where(i => i.TraineeId == user.Id)
                    .OrderByDescending(i => i.CreatedAt)
                    .ThenByDescending(i => i.Id)
                    .Select(i => i.RespondedAt)
                    .FirstOrDefault(),
                StatusOrder = linkedAt != null ? 0
                    : lastInvitationStatus == TrainerInvitationStatus.Pending && lastInvitationExpiresAt > now ? 1
                    : lastInvitationStatus == TrainerInvitationStatus.Pending ? 2
                    : lastInvitationStatus == TrainerInvitationStatus.Expired ? 2
                    : lastInvitationStatus == TrainerInvitationStatus.Rejected ? 3
                    : lastInvitationStatus == TrainerInvitationStatus.Accepted ? 4
                    : lastInvitationStatus == null ? 5
                    : 6
            };
    }

    private IQueryable<DashboardTraineeProjection> ApplySearch(IQueryable<DashboardTraineeProjection> baseQuery, string? searchValue)
    {
        if (string.IsNullOrWhiteSpace(searchValue))
        {
            return baseQuery;
        }

        var search = searchValue.Trim();
        var pattern = $"%{search}%";
        var isNpgsql = _dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
        return isNpgsql
            ? baseQuery.Where(x => EF.Functions.ILike(x.Name, pattern) || EF.Functions.ILike(x.Email, pattern))
            : baseQuery.Where(x => EF.Functions.Like(x.Name, pattern) || EF.Functions.Like(x.Email, pattern));
    }

    private static IQueryable<DashboardTraineeProjection> ApplyStatusFilter(
        IQueryable<DashboardTraineeProjection> baseQuery,
        string? status,
        DateTimeOffset now)
    {
        if (!Enum.TryParse<TrainerDashboardTraineeStatus>(status, true, out var statusFilter))
        {
            return baseQuery;
        }

        return statusFilter switch
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

    private static bool HasPendingInvitation(DashboardTraineeProjection projection, DateTimeOffset now)
    {
        return projection.LinkedAt == null
               && projection.LastInvitationStatus == TrainerInvitationStatus.Pending
               && projection.LastInvitationExpiresAt > now;
    }

    private static bool HasExpiredInvitation(DashboardTraineeProjection projection, DateTimeOffset now)
    {
        return projection.LinkedAt == null
               && (projection.LastInvitationStatus == TrainerInvitationStatus.Expired
                   || (projection.LastInvitationStatus == TrainerInvitationStatus.Pending
                       && projection.LastInvitationExpiresAt <= now));
    }

    private static TrainerDashboardTraineeStatus ResolveTraineeStatus(DashboardTraineeProjection projection, DateTimeOffset now)
    {
        if (projection.LinkedAt != null)
        {
            return TrainerDashboardTraineeStatus.Linked;
        }

        if (projection.LastInvitationStatus == null)
        {
            return TrainerDashboardTraineeStatus.NoRelationship;
        }

        return projection.LastInvitationStatus switch
        {
            TrainerInvitationStatus.Accepted => TrainerDashboardTraineeStatus.InvitationAccepted,
            TrainerInvitationStatus.Rejected => TrainerDashboardTraineeStatus.InvitationRejected,
            TrainerInvitationStatus.Expired => TrainerDashboardTraineeStatus.InvitationExpired,
            TrainerInvitationStatus.Pending when projection.LastInvitationExpiresAt <= now => TrainerDashboardTraineeStatus.InvitationExpired,
            TrainerInvitationStatus.Pending => TrainerDashboardTraineeStatus.InvitationPending,
            _ => TrainerDashboardTraineeStatus.NoRelationship
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

    internal sealed class DashboardTraineeProjection
    {
        public LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User> Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string? Avatar { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? LinkedAt { get; init; }
        public TrainerInvitationStatus? LastInvitationStatus { get; init; }
        public DateTimeOffset? LastInvitationExpiresAt { get; init; }
        public DateTimeOffset? LastInvitationRespondedAt { get; init; }
        public int StatusOrder { get; init; }
    }
}
