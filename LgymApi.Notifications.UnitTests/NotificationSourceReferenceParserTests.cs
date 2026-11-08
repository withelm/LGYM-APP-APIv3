using FluentAssertions;
using LgymApi.Application.Notifications.Authorization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Notifications.UnitTests;

[TestFixture]
public sealed class NotificationSourceReferenceParserTests
{
    private static readonly DateTimeOffset ProducerTimestamp =
        new(2026, 11, 8, 12, 34, 56, 789, TimeSpan.FromHours(1));

    [TestCase("report-feedback", "ReportFeedbackReceived")]
    [TestCase("diet-plan", "DietPlanUpdated")]
    [TestCase("trainee-note", "TraineeNoteUpdated")]
    public void TryParse_AcceptsProducerRoundTripTimestamp(string prefix, string typeName)
    {
        var sourceId = Id<NotificationSourceResource>.New();
        var notification = CreateNotification(
            NotificationType(typeName),
            $"{prefix}:{sourceId}:{ProducerTimestamp:O}");

        var parsed = NotificationSourceReferenceParser.TryParse(notification, out var source);

        parsed.Should().BeTrue();
        source.SourceId.Should().Be(sourceId);
    }

    [TestCase("")]
    [TestCase("arbitrary")]
    [TestCase("2026-11-08T12:34:56+01:00")]
    [TestCase("2026-11-08T12:34:56.7890000+01:00:extra")]
    public void TryParse_RejectsMalformedVersionSuffix(string suffix)
    {
        var sourceId = Id<NotificationSourceResource>.New();
        var notification = CreateNotification(
            InAppNotificationTypes.DietPlanUpdated,
            $"diet-plan:{sourceId}:{suffix}");

        NotificationSourceReferenceParser.TryParse(notification, out _).Should().BeFalse();
    }

    [Test]
    public void TryParse_RejectsVersionedKeyWithoutSender()
    {
        var notification = CreateNotification(
            InAppNotificationTypes.TraineeNoteUpdated,
            $"trainee-note:{Id<TraineeNote>.New()}:{ProducerTimestamp:O}");
        notification.SenderUserId = null;

        NotificationSourceReferenceParser.TryParse(notification, out _).Should().BeFalse();
    }

    [Test]
    public void TryParse_RejectsUnexpectedSuffixOnUnversionedSubmissionKey()
    {
        var notification = CreateNotification(
            InAppNotificationTypes.ReportSubmissionReceived,
            $"report-submission:{Id<ReportSubmission>.New()}:extra");

        NotificationSourceReferenceParser.TryParse(notification, out _).Should().BeFalse();
    }

    private static InAppNotification CreateNotification(InAppNotificationType type, string deliveryKey) => new()
    {
        Id = Id<InAppNotification>.New(),
        RecipientId = Id<User>.New(),
        SenderUserId = Id<User>.New(),
        DeliveryKey = deliveryKey,
        Type = type,
        Message = "source"
    };

    private static InAppNotificationType NotificationType(string name) => name switch
    {
        "ReportFeedbackReceived" => InAppNotificationTypes.ReportFeedbackReceived,
        "DietPlanUpdated" => InAppNotificationTypes.DietPlanUpdated,
        "TraineeNoteUpdated" => InAppNotificationTypes.TraineeNoteUpdated,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
    };
}
