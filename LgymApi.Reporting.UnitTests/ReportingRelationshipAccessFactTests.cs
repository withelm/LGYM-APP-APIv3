using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.Enums;

namespace LgymApi.Reporting.UnitTests;

[TestFixture]
public sealed class ReportingRelationshipAccessFactTests
{
    [Test]
    public void HadRelationshipAt_UsesStartInclusiveEndExclusiveIntervals()
    {
        var start = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);
        var end = start.AddHours(2);
        var access = new ReportingRelationshipAccessFact(
            false,
            [new ReportingRelationshipPeriodFact(start, end)]);

        Assert.That(access.HadRelationshipAt(start), Is.True);
        Assert.That(access.HadRelationshipAt(end.AddTicks(-1)), Is.True);
        Assert.That(access.HadRelationshipAt(end), Is.False);
    }

    [Test]
    public void HadRelationshipAt_WhenStartIsUnknown_DoesNotClaimAnUnboundedRelationship()
    {
        var end = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);
        var access = new ReportingRelationshipAccessFact(
            false,
            [new ReportingRelationshipPeriodFact(
                null,
                end,
                CoachingEpisodeStartProvenance.ExistingActiveLinkObservedDuringMigration)]);

        Assert.That(access.HadRelationshipAt(end.AddYears(-10)), Is.False);
        Assert.That(access.HadRelationshipAt(end), Is.False);
    }

    [Test]
    public void CanReadTrainerAuthoredRecordAt_UsesObservedLegacyProvenanceWithoutClaimingGenericRelationship()
    {
        var end = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);
        var occurredBeforeObservation = end.AddYears(-10);
        var access = new ReportingRelationshipAccessFact(
            false,
            [new ReportingRelationshipPeriodFact(
                null,
                end,
                CoachingEpisodeStartProvenance.ExistingActiveLinkObservedDuringMigration)]);

        Assert.That(access.HadRelationshipAt(occurredBeforeObservation), Is.False);
        Assert.That(access.CanReadTrainerAuthoredRecordAt(occurredBeforeObservation), Is.True);
        Assert.That(access.CanReadTrainerAuthoredRecordAt(end), Is.False);
    }
}
