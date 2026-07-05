using FluentAssertions;
using LgymApi.Application.Features.AdminManagement.Models;
using LgymApi.Application.Models;
using LgymApi.Application.Pagination;
using Microsoft.Extensions.Logging.Abstractions;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Actions;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;

namespace LgymApi.UnitTests.InAppNotifications;

[TestFixture]
public sealed class TrainerInvitationCreatedInAppNotificationCommandHandlerTests
{
    [Test]
    public async Task ExecuteAsync_Success_CreatesExpectedNotification()
    {
        var service = new FakeNotificationService(Result<InAppNotificationResult, AppError>.Success(CreateResult()));
        var trainerId = Id<User>.New();
        var invitationId = Id<TrainerInvitation>.New();
        var handler = new TrainerInvitationCreatedInAppNotificationCommandHandler(
            service,
            new FakeUserRepository(new User { Id = trainerId, Name = "Jan Kowalski" }),
            NullLogger<TrainerInvitationCreatedInAppNotificationCommandHandler>.Instance);
        var command = new TrainerInvitationCreatedInAppNotificationCommand { InvitationId = invitationId, TraineeId = Id<User>.New(), TrainerId = trainerId };

        await handler.ExecuteAsync(command);

        service.Calls.Should().Be(1);
        service.LastInput!.RecipientId.Should().Be(command.TraineeId);
        service.LastInput.SenderUserId.Should().Be(command.TrainerId);
        service.LastInput.DeliveryKey.Should().Be($"trainer-invitation:{invitationId}:sent");
        service.LastInput.IsSystemNotification.Should().BeFalse();
        service.LastInput.Message.Should().Be(string.Format(Messages.TrainerInvitationCreatedNotification, "Jan Kowalski"));
        service.LastInput.RedirectUrl.Should().Be($"/trainers/invitations/{invitationId}");
        service.LastInput.Type.Should().Be(InAppNotificationTypes.InvitationSent);
    }

    [Test]
    public async Task ExecuteAsync_Failure_StillInvokesService()
    {
        var service = new FakeNotificationService(Result<InAppNotificationResult, AppError>.Failure(new BadRequestError("boom")));
        var handler = new TrainerInvitationCreatedInAppNotificationCommandHandler(
            service,
            new FakeUserRepository(new User { Id = Id<User>.New(), Name = "Trainer" }),
            NullLogger<TrainerInvitationCreatedInAppNotificationCommandHandler>.Instance);

        await handler.ExecuteAsync(new TrainerInvitationCreatedInAppNotificationCommand { InvitationId = Id<TrainerInvitation>.New(), TraineeId = Id<User>.New(), TrainerId = Id<User>.New() });

        service.Calls.Should().Be(1);
    }

    private static InAppNotificationResult CreateResult()
        => new(Id<InAppNotification>.New(), Id<User>.New(), "message", null, false, InAppNotificationTypes.InvitationSent, false, null, DateTimeOffset.UtcNow);

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

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly User _user;

        public FakeUserRepository(User user) => _user = user;

        public Task<User?> FindByIdAsync(Id<User> id, CancellationToken cancellationToken = default)
            => Task.FromResult<User?>(id == _user.Id ? _user : null);

        public Task<User?> FindByIdIncludingDeletedAsync(Id<User> id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<User?> FindByIdWithRolesAsync(Id<User> id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<User?> FindByNameAsync(string name, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<User?> FindByNameOrEmailAsync(string name, string email, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<UserRankingEntry>> GetRankingAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AddAsync(User user, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Pagination<UserResult>> GetUsersPaginatedAsync(FilterInput filterInput, bool includeDeleted, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
