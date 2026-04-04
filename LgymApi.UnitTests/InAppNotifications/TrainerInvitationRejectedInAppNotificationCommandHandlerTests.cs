using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Models;
using LgymApi.BackgroundWorker.Actions;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;

namespace LgymApi.UnitTests.InAppNotifications;

[TestFixture]
public sealed class TrainerInvitationRejectedInAppNotificationCommandHandlerTests
{
    [Test]
    public async Task ExecuteAsync_Success_CreatesExpectedNotification()
    {
        var service = new FakeNotificationService(Result<InAppNotificationResult, AppError>.Success(CreateResult()));
        var handler = new TrainerInvitationRejectedInAppNotificationCommandHandler(service, NullLogger<TrainerInvitationRejectedInAppNotificationCommandHandler>.Instance);
        var command = new TrainerInvitationRejectedInAppNotificationCommand { TrainerId = Id<User>.New(), TraineeId = Id<User>.New() };

        await handler.ExecuteAsync(command);

        Assert.That(service.Calls, Is.EqualTo(1));
        Assert.That(service.LastInput!.RecipientId, Is.EqualTo(command.TrainerId));
        Assert.That(service.LastInput.SenderUserId, Is.EqualTo(command.TraineeId));
        Assert.That(service.LastInput.IsSystemNotification, Is.False);
        Assert.That(service.LastInput.Message, Is.EqualTo("Uczeń odrzucił Twoje zaproszenie."));
        Assert.That(service.LastInput.RedirectUrl, Is.EqualTo("/trainers/dashboard"));
        Assert.That(service.LastInput.Type, Is.EqualTo(InAppNotificationTypes.InvitationRejected));
    }

    [Test]
    public async Task ExecuteAsync_Failure_StillInvokesService()
    {
        var service = new FakeNotificationService(Result<InAppNotificationResult, AppError>.Failure(new BadRequestError("boom")));
        var handler = new TrainerInvitationRejectedInAppNotificationCommandHandler(service, NullLogger<TrainerInvitationRejectedInAppNotificationCommandHandler>.Instance);

        await handler.ExecuteAsync(new TrainerInvitationRejectedInAppNotificationCommand { TrainerId = Id<User>.New(), TraineeId = Id<User>.New() });

        Assert.That(service.Calls, Is.EqualTo(1));
    }

    private static InAppNotificationResult CreateResult()
        => new(Id<InAppNotification>.New(), Id<User>.New(), "message", null, false, InAppNotificationTypes.InvitationRejected, false, null, DateTimeOffset.UtcNow);

    private sealed class FakeNotificationService : IInAppNotificationService
    {
        private readonly Result<InAppNotificationResult, AppError> _result;

        public FakeNotificationService(Result<InAppNotificationResult, AppError> result) => _result = result;

        public int Calls { get; private set; }
        public CreateInAppNotificationInput? LastInput { get; private set; }

        public Task<Result<InAppNotificationResult, AppError>> CreateAsync(CreateInAppNotificationInput input, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastInput = input;
            return Task.FromResult(_result);
        }

        public Task<Result<PagedResult<InAppNotificationResult>, AppError>> GetForUserAsync(Id<User> userId, CursorPaginationQuery query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Result<Unit, AppError>> MarkAsReadAsync(Id<InAppNotification> notificationId, Id<User> requestingUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Result<Unit, AppError>> MarkAllAsReadAsync(Id<User> userId, DateTimeOffset? before, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Result<int, AppError>> GetUnreadCountAsync(Id<User> userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
