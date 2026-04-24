using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.Pagination;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Pagination;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed partial class TrainerRelationshipRepository : ITrainerRelationshipRepository
{
    private readonly AppDbContext _dbContext;
    private readonly IGridifyExecutionService _gridifyExecutionService;
    private readonly IMapperRegistry _mapperRegistry;

    private static readonly PaginationPolicy DashboardPaginationPolicy = new()
    {
        MaxPageSize = 100,
        DefaultPageSize = 20,
        DefaultSortField = "name",
        TieBreakerField = "id"
    };

    private static readonly PaginationPolicy InvitationPaginationPolicy = new()
    {
        MaxPageSize = 100,
        DefaultPageSize = 20,
        DefaultSortField = "createdAt",
        TieBreakerField = "id"
    };

    public TrainerRelationshipRepository(
        AppDbContext dbContext,
        IGridifyExecutionService gridifyExecutionService,
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

    public Task<List<TrainerInvitationResult>> GetPendingInvitationsForTraineeAsync(
        Id<User> traineeId,
        string traineeEmail,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        return (
            from invitation in _dbContext.TrainerInvitations.AsNoTracking()
            join trainee in _dbContext.Users.AsNoTracking() on invitation.TraineeId equals trainee.Id into traineeGroup
            from trainee in traineeGroup.DefaultIfEmpty()
            where invitation.Status == TrainerInvitationStatus.Pending
                && invitation.ExpiresAt > now
                && (invitation.TraineeId == traineeId || invitation.InviteeEmail == traineeEmail)
            orderby invitation.CreatedAt descending
            select new TrainerInvitationResult
            {
                Id = invitation.Id,
                TrainerId = invitation.TrainerId,
                TraineeId = invitation.TraineeId,
                InviteeEmail = invitation.InviteeEmail,
                Code = invitation.Code,
                Status = invitation.Status,
                ExpiresAt = invitation.ExpiresAt,
                RespondedAt = invitation.RespondedAt,
                CreatedAt = invitation.CreatedAt,
                TraineeName = trainee.Name,
                TraineeEmail = trainee.Email
            }
        ).ToListAsync(cancellationToken);
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

    public async Task<Pagination<TrainerInvitationResult>> GetInvitationsPaginatedAsync(Id<User> trainerId, FilterInput filterInput, CancellationToken cancellationToken = default)
    {
        var baseQuery = BuildInvitationBaseQuery(trainerId);
        return await _gridifyExecutionService.ExecuteAsync(
            baseQuery,
            filterInput,
            _mapperRegistry,
            InvitationPaginationPolicy,
            cancellationToken);
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

}
