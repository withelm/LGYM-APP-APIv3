using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories.Reporting;

public sealed class ReportingRelationshipAccessPersistenceRepository : IReportingRelationshipAccessPersistence
{
    private readonly AppDbContext _dbContext;

    public ReportingRelationshipAccessPersistenceRepository(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task<ReportingRelationshipAccessFact> GetAccessAsync(
        Id<AccountReference> trainerId,
        Id<AccountReference> traineeId,
        CancellationToken cancellationToken = default)
    {
        var persistedTrainerId = ReportingPersistenceAccountIds.ToPersisted(trainerId);
        var persistedTraineeId = ReportingPersistenceAccountIds.ToPersisted(traineeId);
        var activeEpisode = await _dbContext.TrainerTraineeLinks
            .AsNoTracking()
            .Where(link => link.TrainerId == persistedTrainerId && link.TraineeId == persistedTraineeId && link.EpisodeId != null)
            .Join(
                _dbContext.CoachingEpisodes.AsNoTracking(),
                link => link.EpisodeId,
                episode => (Id<LgymApi.Domain.Entities.CoachingEpisode>?)episode.Id,
                (_, episode) => new ReportingActiveRelationshipFact(episode.Id, episode.AuthorizationRevision))
            .SingleOrDefaultAsync(cancellationToken);
        var relationshipPeriods = await _dbContext.CoachingEpisodes
            .AsNoTracking()
            .Where(episode => episode.TrainerId == persistedTrainerId && episode.TraineeId == persistedTraineeId)
            .Select(episode => new ReportingRelationshipPeriodFact(
                episode.StartedAt,
                episode.EndedAt,
                episode.StartedAtProvenance,
                episode.Id))
            .ToListAsync(cancellationToken);
        return new ReportingRelationshipAccessFact(activeEpisode is not null, relationshipPeriods, activeEpisode);
    }

    public async Task<bool> StageActiveEpisodeRevisionCheckAsync(
        ReportingActiveRelationshipFact activeEpisode,
        CancellationToken cancellationToken = default)
    {
        var episode = await _dbContext.CoachingEpisodes.FirstOrDefaultAsync(candidate =>
            candidate.Id == activeEpisode.EpisodeId
            && candidate.EndedAt == null
            && candidate.AuthorizationRevision == activeEpisode.AuthorizationRevision,
            cancellationToken);
        if (episode is null)
        {
            return false;
        }

        _dbContext.Entry(episode).Property(candidate => candidate.AuthorizationRevision).IsModified = true;
        return true;
    }

    public bool IsActiveEpisodeRevisionConflict(Exception exception)
        => exception is DbUpdateConcurrencyException concurrencyException
            && concurrencyException.Entries.Any(entry => entry.Entity is CoachingEpisode);
}
