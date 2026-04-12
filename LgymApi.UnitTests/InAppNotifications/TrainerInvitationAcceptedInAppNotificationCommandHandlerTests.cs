using FluentAssertions;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Models;
using LgymApi.BackgroundWorker.Actions;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using LgymApi.Resources;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace LgymApi.UnitTests.InAppNotifications;

[TestFixture]
public sealed class TrainerInvitationAcceptedInAppNotificationCommandHandlerTests
{
    [Test]
    public async Task ExecuteAsync_Success_CreatesExpectedNotification()
    {
        var service = new FakeNotificationService(Result<InAppNotificationResult, AppError>.Success(CreateResult()));
        var handler = new TrainerInvitationAcceptedInAppNotificationCommandHandler(service, NullLogger<TrainerInvitationAcceptedInAppNotificationCommandHandler>.Instance);
        var command = new TrainerInvitationAcceptedInAppNotificationCommand { TrainerId = Id<User>.New(), TraineeId = Id<User>.New() };

         await handler.ExecuteAsync(command);

         service.Calls.Should().Be(1);
         service.LastInput!.RecipientId.Should().Be(command.TrainerId);
         service.LastInput.SenderUserId.Should().Be(command.TraineeId);
         service.LastInput.IsSystemNotification.Should().BeFalse();
         service.LastInput.Message.Should().Be(Messages.TrainerInvitationAccepted);
         service.LastInput.RedirectUrl.Should().Be("/trainers/dashboard");
         service.LastInput.Type.Should().Be(InAppNotificationTypes.InvitationAccepted);
    }

    [Test]
    public async Task ExecuteAsync_Failure_StillInvokesService()
    {
        var service = new FakeNotificationService(Result<InAppNotificationResult, AppError>.Failure(new BadRequestError("boom")));
        var handler = new TrainerInvitationAcceptedInAppNotificationCommandHandler(service, NullLogger<TrainerInvitationAcceptedInAppNotificationCommandHandler>.Instance);

         await handler.ExecuteAsync(new TrainerInvitationAcceptedInAppNotificationCommand { TrainerId = Id<User>.New(), TraineeId = Id<User>.New() });

         service.Calls.Should().Be(1);
    }

    private static InAppNotificationResult CreateResult()
        => new(Id<InAppNotification>.New(), Id<User>.New(), "message", null, false, InAppNotificationTypes.InvitationAccepted, false, null, DateTimeOffset.UtcNow);

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
