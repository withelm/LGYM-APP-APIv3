using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.Pagination;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed partial class TrainerRelationshipRepository
{
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
}
