using FluentAssertions;
using LgymApi.Application.Notifications.Authorization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Notifications.UnitTests;

[TestFixture]
public sealed class NotificationSourceEligibilityPolicyTests
{
    [TestCase((int)NotificationSourceState.Pending, true)]
    [TestCase((int)NotificationSourceState.Submitted, false)]
    [TestCase((int)NotificationSourceState.Cancelled, false)]
    [TestCase((int)NotificationSourceState.Expired, false)]
    public void CanDisclose_ReportRequestRequiresPendingState(int stateValue, bool expected)
    {
        var state = (NotificationSourceState)stateValue;
        var source = Reference(NotificationSourceKind.ReportRequest, NotificationSourceState.Pending);

        NotificationSourceEligibilityPolicy.CanDisclose(source, Facts(state: state)).Should().Be(expected);
    }

    [TestCase(true, true, true)]
    [TestCase(true, false, false)]
    [TestCase(false, true, true)]
    [TestCase(false, false, true)]
    public void CanDisclose_ReportRequestRequiresActiveEpisodeOnlyWhenSpecified(
        bool requiresActiveSourceEpisode,
        bool hasActiveMatchingSourceEpisode,
        bool expected)
    {
        var source = Reference(NotificationSourceKind.ReportRequest, NotificationSourceState.Pending) with
        {
            RequiresActiveSourceEpisode = requiresActiveSourceEpisode
        };

        NotificationSourceEligibilityPolicy.CanDisclose(
            source,
            Facts(NotificationSourceState.Pending, hasActiveMatchingSourceEpisode: hasActiveMatchingSourceEpisode))
            .Should().Be(expected);
    }

    [TestCase((int)NotificationSourceState.Pending, false)]
    [TestCase((int)NotificationSourceState.Submitted, true)]
    [TestCase((int)NotificationSourceState.Completed, true)]
    [TestCase((int)NotificationSourceState.Cancelled, false)]
    [TestCase((int)NotificationSourceState.Expired, false)]
    public void CanDisclose_ReportSubmissionRequiresSubmittedState(int stateValue, bool expected)
    {
        var state = (NotificationSourceState)stateValue;
        var source = Reference(NotificationSourceKind.ReportSubmission, NotificationSourceState.Submitted);

        NotificationSourceEligibilityPolicy.CanDisclose(source, Facts(state: state)).Should().Be(expected);
    }

    [TestCase(true, true)]
    [TestCase(false, false)]
    public void CanDisclose_ReportFeedbackAlsoRequiresExpectedContent(bool hasExpectedContent, bool expected)
    {
        var source = Reference(
            NotificationSourceKind.ReportSubmission,
            NotificationSourceState.Submitted,
            requiresVisibleContent: true);

        NotificationSourceEligibilityPolicy.CanDisclose(
            source,
            Facts(state: NotificationSourceState.Submitted, hasExpectedContent: hasExpectedContent)).Should().Be(expected);
    }

    [TestCase(true, true)]
    [TestCase(false, false)]
    public void CanDisclose_ReportFeedbackReadRequiresActiveMatchingSourceEpisode(
        bool hasActiveMatchingSourceEpisode,
        bool expected)
    {
        var source = Reference(NotificationSourceKind.ReportSubmission, NotificationSourceState.Completed) with
        {
            RequiresActiveSourceEpisode = true
        };

        NotificationSourceEligibilityPolicy.CanDisclose(
            source,
            Facts(
                state: NotificationSourceState.Completed,
                hasActiveMatchingSourceEpisode: hasActiveMatchingSourceEpisode)).Should().Be(expected);
    }

    [TestCase(false, true, true)]
    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    public void CanDisclose_RejectsUnavailableSenderRecipientOrMismatchedParticipants(
        bool senderAvailable,
        bool recipientAvailable,
        bool participantsMatch)
    {
        var source = Reference(NotificationSourceKind.ReportSubmission, NotificationSourceState.Submitted);

        NotificationSourceEligibilityPolicy.CanDisclose(
            source,
            Facts(
                state: NotificationSourceState.Submitted,
                senderAvailable: senderAvailable,
                recipientAvailable: recipientAvailable,
                participantsMatch: participantsMatch)).Should().BeFalse();
    }

    private static NotificationSourceReference Reference(
        NotificationSourceKind kind,
        NotificationSourceState requiredState,
        bool requiresVisibleContent = false) => new(
        Id<InAppNotification>.New(),
        kind,
        Id<NotificationSourceResource>.New(),
        Id<User>.New(),
        Id<User>.New(),
        NotificationSourceRecipientRole.Trainee,
        requiredState,
        requiresVisibleContent);

    private static NotificationSourceFacts Facts(
        NotificationSourceState state,
        bool senderAvailable = true,
        bool recipientAvailable = true,
        bool participantsMatch = true,
        bool hasExpectedContent = true,
        bool hasActiveMatchingSourceEpisode = false) => new(
        true,
        false,
        recipientAvailable,
        senderAvailable,
        participantsMatch,
        state,
        true,
        hasExpectedContent,
        false,
        hasActiveMatchingSourceEpisode);
}
