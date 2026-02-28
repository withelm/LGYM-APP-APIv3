using LgymApi.BackgroundWorker.Common.Outbox;
using LgymApi.BackgroundWorker.Outbox;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories;
using LgymApi.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class OutboxPipelineTests
{
    [Test]
    public async Task Dispatcher_CreatesDelivery_And_MarksMessageProcessed()
    {
        await using var dbContext = CreateDbContext();
        var repository = new OutboxRepository(dbContext);
        var unitOfWork = new EfUnitOfWork(dbContext);
        var scheduler = new FakeDeliveryScheduler();

        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "event.test",
            PayloadJson = "{}",
            CorrelationId = Guid.NewGuid(),
            Status = OutboxMessageStatus.Pending
        };

        await repository.AddMessageAsync(message);
        await unitOfWork.SaveChangesAsync();

        var dispatcher = new OutboxDispatcherService(
            repository,
            scheduler,
            unitOfWork,
            [new PassThroughHandler("event.test", "handler.test")],
            NullLogger<OutboxDispatcherService>.Instance);

        await dispatcher.DispatchPendingAsync();

        var savedMessage = await repository.FindMessageByIdAsync(message.Id);
        var delivery = await repository.FindDeliveryAsync(message.Id, "handler.test");

        Assert.Multiple(() =>
        {
            Assert.That(savedMessage, Is.Not.Null);
            Assert.That(savedMessage!.Status, Is.EqualTo(OutboxMessageStatus.Processed));
            Assert.That(delivery, Is.Not.Null);
            Assert.That(delivery!.Status, Is.EqualTo(OutboxDeliveryStatus.Pending));
            Assert.That(scheduler.EnqueuedIds, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task DeliveryProcessor_WhenHandlerSucceeds_MarksSucceeded()
    {
        await using var dbContext = CreateDbContext();
        var repository = new OutboxRepository(dbContext);
        var unitOfWork = new EfUnitOfWork(dbContext);

        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "event.success",
            PayloadJson = "{}",
            CorrelationId = Guid.NewGuid(),
            Status = OutboxMessageStatus.Processed
        };

        var delivery = new OutboxDelivery
        {
            Id = Guid.NewGuid(),
            EventId = message.Id,
            HandlerName = "handler.success",
            Status = OutboxDeliveryStatus.Pending
        };

        await repository.AddMessageAsync(message);
        await repository.AddDeliveryAsync(delivery);
        await unitOfWork.SaveChangesAsync();

        var processor = new OutboxDeliveryProcessorService(
            repository,
            unitOfWork,
            [new PassThroughHandler("event.success", "handler.success")],
            NullLogger<OutboxDeliveryProcessorService>.Instance);

        await processor.ProcessAsync(delivery.Id);

        var savedDelivery = await repository.FindDeliveryByIdWithEventAsync(delivery.Id);
        Assert.Multiple(() =>
        {
            Assert.That(savedDelivery, Is.Not.Null);
            Assert.That(savedDelivery!.Status, Is.EqualTo(OutboxDeliveryStatus.Succeeded));
            Assert.That(savedDelivery.ProcessedAt, Is.Not.Null);
            Assert.That(savedDelivery.LastError, Is.Null);
        });
    }

    [Test]
    public async Task DeliveryProcessor_WhenHandlerFails_SchedulesRetry()
    {
        await using var dbContext = CreateDbContext();
        var repository = new OutboxRepository(dbContext);
        var unitOfWork = new EfUnitOfWork(dbContext);

        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "event.fail",
            PayloadJson = "{}",
            CorrelationId = Guid.NewGuid(),
            Status = OutboxMessageStatus.Processed
        };

        var delivery = new OutboxDelivery
        {
            Id = Guid.NewGuid(),
            EventId = message.Id,
            HandlerName = "handler.fail",
            Status = OutboxDeliveryStatus.Pending
        };

        await repository.AddMessageAsync(message);
        await repository.AddDeliveryAsync(delivery);
        await unitOfWork.SaveChangesAsync();

        var processor = new OutboxDeliveryProcessorService(
            repository,
            unitOfWork,
            [new ThrowingHandler("event.fail", "handler.fail")],
            NullLogger<OutboxDeliveryProcessorService>.Instance);

        await processor.ProcessAsync(delivery.Id);

        var savedDelivery = await repository.FindDeliveryByIdWithEventAsync(delivery.Id);
        Assert.Multiple(() =>
        {
            Assert.That(savedDelivery, Is.Not.Null);
            Assert.That(savedDelivery!.Status, Is.EqualTo(OutboxDeliveryStatus.Pending));
            Assert.That(savedDelivery.NextAttemptAt, Is.Not.Null);
            Assert.That(savedDelivery.LastError, Does.StartWith("InvalidOperationException"));
            Assert.That(savedDelivery.Attempts, Is.EqualTo(1));
        });
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"outbox-tests-{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private sealed class FakeDeliveryScheduler : IOutboxDeliveryBackgroundScheduler
    {
        public List<Guid> EnqueuedIds { get; } = new();

        public void Enqueue(Guid deliveryId)
        {
            EnqueuedIds.Add(deliveryId);
        }
    }

    private sealed class PassThroughHandler : IOutboxDeliveryHandler
    {
        public PassThroughHandler(string eventType, string handlerName)
        {
            EventType = eventType;
            HandlerName = handlerName;
        }

        public string EventType { get; }
        public string HandlerName { get; }

        public Task HandleAsync(Guid eventId, Guid correlationId, string payloadJson, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler : IOutboxDeliveryHandler
    {
        public ThrowingHandler(string eventType, string handlerName)
        {
            EventType = eventType;
            HandlerName = handlerName;
        }

        public string EventType { get; }
        public string HandlerName { get; }

        public Task HandleAsync(Guid eventId, Guid correlationId, string payloadJson, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("failure");
        }
    }
}
