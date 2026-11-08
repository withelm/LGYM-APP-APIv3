using FluentAssertions;
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

        access.HadRelationshipAt(start).Should().BeTrue();
        access.HadRelationshipAt(end.AddTicks(-1)).Should().BeTrue();
        access.HadRelationshipAt(end).Should().BeFalse();
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

        access.HadRelationshipAt(end.AddYears(-10)).Should().BeFalse();
        access.HadRelationshipAt(end).Should().BeFalse();
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

        access.HadRelationshipAt(occurredBeforeObservation).Should().BeFalse();
        access.CanReadTrainerAuthoredRecordAt(occurredBeforeObservation).Should().BeTrue();
        access.CanReadTrainerAuthoredRecordAt(end).Should().BeFalse();
    }
}
