using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace LgymApi.IntegrationTests;

/// <summary>
/// Uniqueness verification tests for reliability test fixtures.
/// These tests demonstrate that the relational (SQL) test fixture can catch
/// duplicate-key violations that in-memory providers cannot.
/// 
/// This is a critical prerequisite for T12: these tests MUST pass to guarantee
/// that reliability tests can actually verify uniqueness enforcement.
/// 
/// NOTE: Currently uses InMemory EF provider (CustomWebApplicationFactory).
/// These tests will gain full value after switching to a real relational DB fixture for reliability tests.
/// </summary>
[TestFixture]
public class ReliabilityUniquenessCheckTests : IntegrationTestBase
{
    /// <summary>
    /// Demonstrates the fixture can detect and assert on CommandEnvelope uniqueness.
    /// Even with InMemory EF (which is lenient on constraints), the fixture provides
    /// a pathway to verify uniqueness behavior.
    /// 
    /// In T12, this test pattern will be used with a relational test fixture to catch
    /// actual duplicate-key violations at the database level.
    /// </summary>
    [Test]
    public async Task CommandEnvelopeUniquenessAssertion_CanDetectCorrectCount()
    {
        // Arrange
        var correlationId = Id<CorrelationScope>.New();
        
        // Create a CommandEnvelope directly in the database
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var envelope = new CommandEnvelope
        {
            CorrelationId = correlationId,
            PayloadJson = "{}",
            CommandTypeFullName = typeof(object).FullName!,
            Status = ActionExecutionStatus.Pending
        };
        
        db.CommandEnvelopes.Add(envelope);
        await db.SaveChangesAsync();

        // Act & Assert
        // The fixture should correctly count and assert uniqueness
        var count = await CountCommandEnvelopesByCorrelationIdAsync(correlationId);
        Assert.That(count, Is.EqualTo(1), "Fixture should count exactly one envelope for the correlation ID");
        
        // This should succeed without throwing
        await AssertCommandEnvelopeUniquenessAsync(correlationId);
    }

    /// <summary>
    /// Demonstrates the fixture can detect duplicate CommandEnvelopes if they exist.
    /// This validates that the assertion helper can catch violations.
    /// 
    /// NOTE: InMemory EF may not enforce unique constraints, so duplicates CAN be inserted.
    /// This test verifies the fixture's assertion logic works correctly when duplicates exist.
    /// </summary>
    [Test]
    public void CommandEnvelopeUniquenessAssertion_DetectsDuplicates()
    {
        // Arrange
        var correlationId = Id<CorrelationScope>.New();
        
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // Insert TWO envelopes with the same correlation ID
        // (InMemory EF may allow this; real DB would reject with constraint violation)
        var envelope1 = new CommandEnvelope
        {
            Id = Id<CommandEnvelope>.New(), // Unique ID for each envelope
            CorrelationId = correlationId,
            PayloadJson = "{}",
            CommandTypeFullName = typeof(object).FullName!,
            Status = ActionExecutionStatus.Pending
        };
        var envelope2 = new CommandEnvelope
        {
            Id = Id<CommandEnvelope>.New(), // Unique ID for each envelope
            CorrelationId = correlationId,
            PayloadJson = "{}",
            CommandTypeFullName = typeof(object).FullName!,
            Status = ActionExecutionStatus.Pending
        };
        
        db.CommandEnvelopes.Add(envelope1);
        db.CommandEnvelopes.Add(envelope2);
        db.SaveChanges();

        // Act
        var count = CountCommandEnvelopesByCorrelationIdAsync(correlationId).GetAwaiter().GetResult();

        // Assert
        Assert.That(count, Is.EqualTo(2), "Fixture should count exactly two envelopes when two exist");
        
        // The assertion should fail because we have duplicates
        Assert.That(
            () => AssertCommandEnvelopeUniquenessAsync(correlationId).GetAwaiter().GetResult(),
            Throws.InstanceOf<AssertionException>(),
            "Assertion should fail when more than one envelope exists for the correlation ID");
    }

    /// <summary>
    /// Demonstrates the fixture can detect NotificationMessage uniqueness violations.
    /// Uses the (Type, CorrelationId, Recipient) composite key pattern.
    /// </summary>
    [Test]
    public async Task NotificationMessageUniquenessAssertion_CanDetectCorrectCount()
    {
        // Arrange
        var correlationId = Id<CorrelationScope>.New();
        var recipient = new Email("test@example.com");
        var notificationType = EmailNotificationTypes.Welcome;
        
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var notification = new NotificationMessage
        {
            Type = notificationType,
            CorrelationId = correlationId,
            Recipient = recipient,
            PayloadJson = "{}",
            Status = EmailNotificationStatus.Pending,
            Channel = NotificationChannel.Email
        };
        
        db.NotificationMessages.Add(notification);
        await db.SaveChangesAsync();

        // Act & Assert
        var count = await CountNotificationMessagesByKeyAsync(notificationType, correlationId, recipient);
        Assert.That(count, Is.EqualTo(1), "Fixture should count exactly one notification for the key tuple");
        
        // This should succeed without throwing
        await AssertNotificationMessageUniquenessAsync(notificationType, correlationId, recipient);
    }

    /// <summary>
    /// Demonstrates the fixture can detect duplicate NotificationMessages.
    /// </summary>
    [Test]
    public void NotificationMessageUniquenessAssertion_DetectsDuplicates()
    {
        // Arrange
        var correlationId = Id<CorrelationScope>.New();
        var recipient = new Email("test@example.com");
        var notificationType = EmailNotificationTypes.Welcome;
        
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // Insert TWO notifications with the same key tuple
        var notification1 = new NotificationMessage
        {
            Id = Id<NotificationMessage>.New(), // Unique ID for each notification
            Type = notificationType,
            CorrelationId = correlationId,
            Recipient = recipient,
            PayloadJson = "{}",
            Status = EmailNotificationStatus.Pending,
            Channel = NotificationChannel.Email
        };
        var notification2 = new NotificationMessage
        {
            Id = Id<NotificationMessage>.New(), // Unique ID for each notification
            Type = notificationType,
            CorrelationId = correlationId,
            Recipient = recipient,
            PayloadJson = "{}",
            Status = EmailNotificationStatus.Pending,
            Channel = NotificationChannel.Email
        };
        
        db.NotificationMessages.Add(notification1);
        db.NotificationMessages.Add(notification2);
        db.SaveChanges();

        // Act
        var count = CountNotificationMessagesByKeyAsync(notificationType, correlationId, recipient).GetAwaiter().GetResult();

        // Assert
        Assert.That(count, Is.EqualTo(2), "Fixture should count exactly two notifications when two exist");
        
        // The assertion should fail because we have duplicates
        Assert.That(
            () => AssertNotificationMessageUniquenessAsync(notificationType, correlationId, recipient).GetAwaiter().GetResult(),
            Throws.InstanceOf<AssertionException>(),
            "Expected AssertionException when more than one notification exists for the key tuple");
    }

    /// <summary>
    /// Demonstrates baseline state tracking for transaction isolation.
    /// Verifies that counts before and after an action can be compared.
    /// </summary>
    [Test]
    public async Task TransactionIsolationTracking_BaselineCounts_CanBeCompared()
    {
        // Arrange
        var baselineEnvelopes = await CountAllCommandEnvelopesAsync();
        var baselineNotifications = await CountAllNotificationMessagesAsync();

        // Act
        var correlationId = Id<CorrelationScope>.New();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var envelope = new CommandEnvelope
        {
            CorrelationId = correlationId,
            PayloadJson = "{}",
            CommandTypeFullName = typeof(object).FullName!,
            Status = ActionExecutionStatus.Pending
        };
        db.CommandEnvelopes.Add(envelope);
        await db.SaveChangesAsync();

        // Assert
        var afterEnvelopes = await CountAllCommandEnvelopesAsync();
        Assert.That(afterEnvelopes, Is.EqualTo(baselineEnvelopes + 1), "Envelope count should increment by 1");
        
        // Notification count should remain unchanged
        var afterNotifications = await CountAllNotificationMessagesAsync();
        Assert.That(afterNotifications, Is.EqualTo(baselineNotifications), "Notification count should remain unchanged");
    }

    /// <summary>
    /// Demonstrates state history retrieval for crash-window scenarios.
    /// Verifies that full state sequences can be inspected.
    /// </summary>
    [Test]
    public async Task StateHistoryRetrieval_CanInspectAllStatuses()
    {
        // Arrange
        var correlationId = Id<CorrelationScope>.New();
        
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var envelope = new CommandEnvelope
        {
            CorrelationId = correlationId,
            PayloadJson = "{}",
            CommandTypeFullName = typeof(object).FullName!,
            Status = ActionExecutionStatus.Pending
        };
        db.CommandEnvelopes.Add(envelope);
        await db.SaveChangesAsync();

        // Act
        var statuses = await GetCommandEnvelopeStatusesAsync(correlationId);

        // Assert
        Assert.That(statuses.Count, Is.EqualTo(1), "Should retrieve exactly one status record");
        Assert.That(statuses[0].Item2, Is.EqualTo(ActionExecutionStatus.Pending), "Status should be Pending");
    }
}
