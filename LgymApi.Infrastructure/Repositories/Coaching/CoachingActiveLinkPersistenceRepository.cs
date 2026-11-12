using LgymApi.Application.Coaching.Contracts.Access;
using LgymApi.Application.Coaching.Persistence;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Identity.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LgymApi.Infrastructure.Repositories.Coaching;

public sealed class CoachingActiveLinkPersistenceRepository : ICoachingActiveLinkPersistence
{
    private readonly AppDbContext _dbContext;
    private readonly IMapper _mapper;

    public CoachingActiveLinkPersistenceRepository(AppDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    public Task AddAsync(CoachingActiveLinkWriteModel link, CancellationToken cancellationToken = default)
    {
        var episodeId = link.EpisodeId.IsEmpty ? Id<CoachingEpisode>.New() : link.EpisodeId;
        var startedAt = link.StartedAt == default ? DateTimeOffset.UtcNow : link.StartedAt;
        var normalizedLink = link with { EpisodeId = episodeId, StartedAt = startedAt };
        _dbContext.CoachingEpisodes.Add(new CoachingEpisode
        {
            Id = normalizedLink.EpisodeId,
            TrainerId = normalizedLink.TrainerId,
            TraineeId = normalizedLink.TraineeId,
            StartedAt = normalizedLink.StartedAt,
            StartedAtProvenance = CoachingEpisodeStartProvenance.ServerAcceptedInvitation,
            AuthorizationRevision = 0
        });
        var entity = _mapper.Map<CoachingActiveLinkWriteModel, TrainerTraineeLink>(normalizedLink, _mapper.CreateContext());
        return _dbContext.TrainerTraineeLinks.AddAsync(entity, cancellationToken).AsTask();
    }

    public async Task<bool> StageRevisionCheckAsync(
        CoachingEpisodeAuthorizationGrant grant,
        CancellationToken cancellationToken = default)
    {
        var episode = await _dbContext.CoachingEpisodes.FirstOrDefaultAsync(
            candidate => candidate.Id == grant.EpisodeId
                && candidate.TrainerId == grant.TrainerId
                && candidate.TraineeId == grant.TraineeId
                && candidate.EndedAt == null
                && candidate.AuthorizationRevision == grant.AuthorizationRevision,
            cancellationToken);
        if (episode is null)
        {
            return false;
        }

        _dbContext.Entry(episode).Property(candidate => candidate.AuthorizationRevision).IsModified = true;
        return true;
    }

    public async Task RemoveAsync(
        CoachingEpisodeAuthorizationGrant grant,
        CancellationToken cancellationToken = default)
    {
        var link = await _dbContext.TrainerTraineeLinks.FirstOrDefaultAsync(
                candidate => candidate.Id == grant.LinkId
                    && candidate.EpisodeId == grant.EpisodeId,
                cancellationToken);
        var episode = await _dbContext.CoachingEpisodes.FirstOrDefaultAsync(
            candidate => candidate.Id == grant.EpisodeId,
            cancellationToken);
        if (link is null
            || episode is null
            || episode.EndedAt is not null
            || episode.AuthorizationRevision != grant.AuthorizationRevision)
        {
            return;
        }

        episode.EndedAt = DateTimeOffset.UtcNow;
        episode.AuthorizationRevision++;
        _dbContext.TrainerTraineeLinks.Remove(link);
    }

    public async Task<bool> TryRecoverCompletedUnlinkAsync(
        CoachingEpisodeAuthorizationGrant grant,
        string envelopeId,
        Exception commitFailure,
        CancellationToken cancellationToken = default)
    {
        var concurrencyConflict = commitFailure is DbUpdateConcurrencyException concurrencyFailure
            && concurrencyFailure.Entries.Any(entry => entry.Entity is CoachingEpisode);
        var envelopeConflict = commitFailure is DbUpdateException
        {
            InnerException: PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_CommandEnvelopes_CorrelationId"
            }
        };
        if (!concurrencyConflict && !envelopeConflict)
        {
            return false;
        }

        var linkStillActive = await _dbContext.TrainerTraineeLinks.AsNoTracking().AnyAsync(
            link => link.Id == grant.LinkId,
            cancellationToken);
        var closedEpisode = await _dbContext.CoachingEpisodes.AsNoTracking().AnyAsync(
            episode => episode.Id == grant.EpisodeId
                && episode.EndedAt != null
                && episode.AuthorizationRevision > grant.AuthorizationRevision,
            cancellationToken);
        var stagedEnvelope = _dbContext.ChangeTracker.Entries<CommandEnvelope>()
            .FirstOrDefault(entry => entry.Entity.Id.ToString() == envelopeId)?.Entity;
        var cancellationCommitted = stagedEnvelope is not null && await _dbContext.CommandEnvelopes.AsNoTracking()
            .AnyAsync(envelope => envelope.CorrelationId == stagedEnvelope.CorrelationId, cancellationToken);
        if (linkStillActive || !closedEpisode || !cancellationCommitted)
        {
            return false;
        }

        foreach (var entry in _dbContext.ChangeTracker.Entries().Where(entry =>
            entry.Entity is CoachingEpisode episode && episode.Id == grant.EpisodeId
            || entry.Entity is TrainerTraineeLink link && link.Id == grant.LinkId
            || entry.Entity is CommandEnvelope envelope && envelope.Id.ToString() == envelopeId).ToList())
        {
            entry.State = EntityState.Detached;
        }

        return true;
    }

    public async Task<bool> TryRecoverDuplicateLinkAsync(
        Id<User> traineeId,
        Exception commitFailure,
        CancellationToken cancellationToken = default)
    {
        if (commitFailure is not DbUpdateException
            {
                InnerException: PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: "IX_TrainerTraineeLinks_TraineeId"
                }
            })
        {
            return false;
        }

        return await _dbContext.TrainerTraineeLinks.AsNoTracking().AnyAsync(
            link => link.TraineeId == traineeId,
            cancellationToken);
    }

    public Task<bool> HasForTraineeAsync(Id<User> traineeId, CancellationToken cancellationToken = default)
        => _dbContext.TrainerTraineeLinks.AnyAsync(link => link.TraineeId == traineeId, cancellationToken);

    public Task<CoachingActiveLinkFact?> FindByTrainerAndTraineeAsync(Id<User> trainerId, Id<User> traineeId, CancellationToken cancellationToken = default)
        => FindAsync(
            _dbContext.TrainerTraineeLinks
                .AsNoTracking()
                .Where(link => link.TrainerId == trainerId && link.TraineeId == traineeId),
            cancellationToken);

    public Task<CoachingActiveLinkFact?> FindByTraineeAsync(Id<User> traineeId, CancellationToken cancellationToken = default)
        => FindAsync(_dbContext.TrainerTraineeLinks.AsNoTracking().Where(link => link.TraineeId == traineeId), cancellationToken);

    public Task<bool> HasActiveRelationshipAsync(Id<AccountReference> trainerId, Id<AccountReference> traineeId, CancellationToken cancellationToken = default)
    {
        var persistedTrainerId = trainerId.Rebind<User>();
        var persistedTraineeId = traineeId.Rebind<User>();
        return _dbContext.TrainerTraineeLinks.AsNoTracking().AnyAsync(
            link => link.TrainerId == persistedTrainerId && link.TraineeId == persistedTraineeId,
            cancellationToken);
    }

    public async Task<CoachingMeasurementsRelationshipAccessFact> GetMeasurementsAccessAsync(
        Id<AccountReference> trainerId,
        Id<AccountReference> traineeId,
        CancellationToken cancellationToken = default)
    {
        var persistedTrainerId = trainerId.Rebind<User>();
        var persistedTraineeId = traineeId.Rebind<User>();
        var rows = await _dbContext.CoachingEpisodes.AsNoTracking()
            .Where(episode => episode.TrainerId == persistedTrainerId && episode.TraineeId == persistedTraineeId)
            .Select(episode => new
            {
                Episode = episode,
                IsActive = _dbContext.TrainerTraineeLinks.AsNoTracking().Any(link =>
                    link.TrainerId == persistedTrainerId
                    && link.TraineeId == persistedTraineeId
                    && link.EpisodeId == episode.Id)
            })
            .ToListAsync(cancellationToken);

        var mappingContext = _mapper.CreateContext();
        var episodes = rows
            .Select(row => _mapper.Map<CoachingEpisode, CoachingEpisodeFact>(row.Episode, mappingContext))
            .ToArray();
        return _mapper.Map<(bool, IReadOnlyList<CoachingEpisodeFact>), CoachingMeasurementsRelationshipAccessFact>(
            (rows.Any(row => row.IsActive), episodes),
            mappingContext);
    }

    private async Task<CoachingActiveLinkFact?> FindAsync(IQueryable<TrainerTraineeLink> query, CancellationToken cancellationToken)
    {
        var link = await query.FirstOrDefaultAsync(cancellationToken);
        if (link?.EpisodeId is not null)
        {
            await _dbContext.Entry(link).Reference(candidate => candidate.Episode).LoadAsync(cancellationToken);
        }
        return link is null
            ? null
            : _mapper.Map<TrainerTraineeLink, CoachingActiveLinkFact>(link, _mapper.CreateContext());
    }
}
