using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.TestUtils;

public sealed record AuthQ01Fixture(
    int SchemaVersion,
    string FixtureId,
    bool SyntheticOnly,
    IReadOnlyList<AuthQ01Actor> Actors,
    AuthQ01Timeline Timeline,
    AuthQ01Relationships RelationshipScenarios,
    IReadOnlyList<AuthQ01Report> Reports,
    IReadOnlyList<AuthQ01NoteVersion> NoteVersions,
    IReadOnlyList<AuthQ01Diet> Diets,
    IReadOnlyList<AuthQ01Plan> Plans,
    IReadOnlyList<AuthQ01Measurement> Measurements,
    IReadOnlyList<AuthQ01ShareCode> ShareCodes,
    IReadOnlyList<AuthQ01Photo> Photos,
    string SyntheticPngBase64)
{
    public AuthQ01Actor Actor(string alias) => Actors.Single(actor => actor.Alias == alias);

    public void Validate()
    {
        if (SchemaVersion != 1 || FixtureId != "AUTH-Q01-v1" || !SyntheticOnly)
        {
            throw new InvalidOperationException("AUTH-Q01 fixture identity or synthetic-only marker is invalid.");
        }

        var aliases = Actors.Select(actor => actor.Alias).ToArray();
        if (!aliases.SequenceEqual(["U", "T1", "T2", "T3", "Admin", "AdminTrainer", "A", "B", "C"]))
        {
            throw new InvalidOperationException("AUTH-Q01 fixture actors must contain the canonical aliases in order.");
        }

        if (Actors.Select(actor => actor.Id).Distinct().Count() != Actors.Count
            || Actors.Any(actor => !Id<AuthQ01Actor>.TryParse(actor.Id, out _)
                || actor.Roles.Count == 0
                || !actor.Roles.Contains("User", StringComparer.Ordinal)
                || actor.Roles.Any(role => role is not ("User" or "Trainer" or "Admin" or "Tester"))))
        {
            throw new InvalidOperationException("AUTH-Q01 actor IDs must be unique UUIDs.");
        }

        var actorAliases = aliases.ToHashSet(StringComparer.Ordinal);
        foreach (var relationship in RelationshipScenarios.All())
        {
            if (!actorAliases.Contains(relationship.Trainer) || !actorAliases.Contains(relationship.Trainee))
            {
                throw new InvalidOperationException("AUTH-Q01 relationships must reference declared actors.");
            }
        }

        if (Timeline.StartedAt >= Timeline.ReportAcceptedAt
            || Timeline.ReportAcceptedAt >= Timeline.RelationshipEndedAt
            || Timeline.RelationshipEndedAt >= Timeline.MeasurementPersistedAt
            || Timeline.MeasurementPersistedAt >= Timeline.PostUnlinkObservedAt
            || Timeline.PostUnlinkObservedAt >= Timeline.ReturnedAt
            || Timeline.ReturnedAt >= Timeline.ReferenceNow)
        {
            throw new InvalidOperationException("AUTH-Q01 timeline must be strictly chronological.");
        }

        var planIds = Plans.Select(plan => plan.Id).ToHashSet(StringComparer.Ordinal);
        var reportIds = Reports.Select(report => report.Id).ToHashSet(StringComparer.Ordinal);
        if (planIds.Count != Plans.Count || reportIds.Count != Reports.Count
            || Plans.Any(plan => !Id<AuthQ01Plan>.TryParse(plan.Id, out _))
            || Reports.Any(report => !Id<AuthQ01Report>.TryParse(report.Id, out _))
            || Plans.Any(plan => !actorAliases.Contains(plan.Owner))
            || Reports.Any(report => !actorAliases.Contains(report.SourceTrainer) || !actorAliases.Contains(report.Trainee))
            || NoteVersions.Select(note => note.Version).Distinct().Count() != NoteVersions.Count
            || NoteVersions.Any(note => !actorAliases.Contains(note.Author) || !actorAliases.Contains(note.Trainee) || string.IsNullOrWhiteSpace(note.Content))
            || Diets.Any(diet => !actorAliases.Contains(diet.Author) || !actorAliases.Contains(diet.Trainee) || (diet.EndDate is not null && diet.EndDate < diet.StartDate))
            || Photos.Any(photo => !reportIds.Contains(photo.SourceReportId))
            || ShareCodes.Any(code => !planIds.Contains(code.SourcePlanId)))
        {
            throw new InvalidOperationException("AUTH-Q01 domain rows must have unique IDs and valid actor/source references.");
        }

        var acceptedReport = Reports.Single(report => report.Id == Measurements.Single(measurement => measurement.SourceReportId != null).SourceReportId);
        var delayedMeasurement = Measurements.Single(measurement => measurement.SourceReportId != null && measurement.PersistedAt > Timeline.RelationshipEndedAt);
        if (acceptedReport.AcceptedAt >= Timeline.RelationshipEndedAt
            || delayedMeasurement.AcceptedAt >= Timeline.RelationshipEndedAt
            || delayedMeasurement.PersistedAt <= Timeline.RelationshipEndedAt)
        {
            throw new InvalidOperationException("AUTH-Q01 delayed measurement provenance must preserve pre-unlink acceptance and post-unlink persistence.");
        }

        try
        {
            _ = Convert.FromBase64String(SyntheticPngBase64);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("AUTH-Q01 synthetic PNG payload must be valid base64.", exception);
        }
    }

}

public sealed record AuthQ01Actor(string Alias, string Id, string Email, IReadOnlyList<string> Roles);
public sealed record AuthQ01Timeline(DateTimeOffset StartedAt, DateTimeOffset ReportAcceptedAt, DateTimeOffset RelationshipEndedAt, DateTimeOffset MeasurementPersistedAt, DateTimeOffset PostUnlinkObservedAt, DateTimeOffset ReturnedAt, DateTimeOffset ReferenceNow);
public sealed record AuthQ01Relationship(string Trainer, string Trainee);
public sealed record AuthQ01Relationships(IReadOnlyList<AuthQ01Relationship> Initial, IReadOnlyList<AuthQ01Relationship> Successor, IReadOnlyList<AuthQ01Relationship> Coachless, IReadOnlyList<AuthQ01Relationship> Returned, IReadOnlyList<AuthQ01Relationship> Chain)
{
    public IEnumerable<AuthQ01Relationship> All() => Initial.Concat(Successor).Concat(Coachless).Concat(Returned).Concat(Chain);
}
public sealed record AuthQ01Report(string Id, string SourceTrainer, string Trainee, DateTimeOffset AcceptedAt, string Answer);
public sealed record AuthQ01NoteVersion(int Version, string Author, string Trainee, bool VisibleToTrainee, string Content);
public sealed record AuthQ01Diet(string Id, string Author, string Trainee, bool IsActive, DateOnly StartDate, DateOnly? EndDate);
public sealed record AuthQ01Plan(string Id, string Owner, string Name);
public sealed record AuthQ01Measurement(string? SourceReportId, DateTimeOffset ObservedAt, DateTimeOffset AcceptedAt, DateTimeOffset PersistedAt, double Value);
public sealed record AuthQ01ShareCode(string Code, string SourcePlanId, DateTimeOffset ExpiresAt);
public sealed record AuthQ01Photo(string Id, string SourceReportId, string StorageKey, string ThumbnailKey);

public static class AuthQ01FixtureLoader
{
    private const string RelativePath = "docs/testing/auth-q01-fixture.json";

    public static AuthQ01Fixture Load(string? repositoryRoot = null)
    {
        var root = repositoryRoot ?? FindRepositoryRoot();
        var path = Path.Combine(root, RelativePath);
        var json = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        if (digest != "F587212EB99B84E07BB8D414B047ECD130A17F0457855AF869E99E23A9D87214")
        {
            throw new InvalidOperationException("AUTH-Q01-v1 differs from the shared API/Page/Mobile fixture. Version and update all copies together.");
        }

        var fixture = JsonSerializer.Deserialize<AuthQ01Fixture>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException($"Could not deserialize '{RelativePath}'.");
        fixture.Validate();
        return fixture;
    }

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "LgymApi.slnx")))
                {
                    return directory.FullName;
                }
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root for AUTH-Q01 fixture.");
    }
}
