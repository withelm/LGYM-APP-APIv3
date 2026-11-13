using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;

namespace LgymApi.Application.Reporting.Persistence;

public interface IReportingRelationshipAccessPersistence
{
    Task<ReportingRelationshipAccessFact> GetAccessAsync(
        Id<AccountReference> trainerId,
        Id<AccountReference> traineeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<(Id<AccountReference> TrainerId, Id<AccountReference> TraineeId), ReportingRelationshipAccessFact>> GetActiveAccessForPairsAsync(
        IReadOnlyCollection<(Id<AccountReference> TrainerId, Id<AccountReference> TraineeId)> pairs,
        CancellationToken cancellationToken = default);

    Task<bool> StageActiveEpisodeRevisionCheckAsync(
        ReportingActiveRelationshipFact activeEpisode,
        CancellationToken cancellationToken = default);

    bool IsActiveEpisodeRevisionConflict(Exception exception);
}
