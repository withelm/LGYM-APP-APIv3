using System.Globalization;
using FluentAssertions;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.AdminManagement.Models;
using LgymApi.Application.Models;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Options;
using LgymApi.Application.Pagination;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Actions;
using LgymApi.Application.Coaching.Contracts.BackgroundCommands;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;

namespace LgymApi.UnitTests.InAppNotifications;

[TestFixture]
public sealed class TraineeNoteUpdatedInAppNotificationCommandHandlerTests
{
    [Test]
    public async Task ExecuteAsync_UsesFallbackNamesAndNoteNotificationContract()
    {
        var previousCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pl-PL");

        try
        {
            var notificationService = new FakeNotificationService(Result<InAppNotificationResult, AppError>.Success(CreateResult()));
            var traineeId = Id<User>.New();
            var noteId = Id<TraineeNote>.New();
            var trainee = new User { Id = traineeId, Name = "Trainee", Email = "trainee@example.com", PreferredLanguage = "pl-PL" };
            var handler = new TraineeNoteUpdatedInAppNotificationCommandHandler(
                notificationService,
                new FakeUserRepository(trainee),
                new AppDefaultsOptions { PreferredLanguage = "en" },
                NullLogger<TraineeNoteUpdatedInAppNotificationCommandHandler>.Instance);

            await handler.ExecuteAsync(new TraineeNoteUpdatedInAppNotificationCommand
            {
                TraineeNoteId = noteId,
                TraineeId = traineeId,
                TrainerId = Id<User>.New(),
                NoteTitle = "   ",
                TriggeredAt = new DateTimeOffset(2026, 6, 26, 0, 30, 0, TimeSpan.Zero)
            });

            notificationService.Calls.Should().Be(1);
            notificationService.LastInput.Should().NotBeNull();
            notificationService.LastInput!.DeliveryKey.Should().Be($"trainee-note:{noteId}:2026-06-26T00:30:00.0000000+00:00");
            notificationService.LastInput.Type.Should().Be(InAppNotificationTypes.TraineeNoteUpdated);
            notificationService.LastInput.Message.Should().Contain(LgymApi.Resources.Messages.GenericTrainerDisplayName);
            notificationService.LastInput.Message.Should().Contain(LgymApi.Resources.Messages.GenericTrainerNoteDisplayName);
            notificationService.LastInput.RedirectUrl.Should().Be($"/trainer/notes/{noteId}");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }

    private static InAppNotificationResult CreateResult()
        => new(Id<InAppNotification>.New(), Id<User>.New(), "message", null, false, InAppNotificationTypes.TraineeNoteUpdated, false, null, DateTimeOffset.UtcNow);

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
        private readonly Dictionary<Id<User>, User> _users;

        public FakeUserRepository(params User[] users)
        {
            _users = users.ToDictionary(user => user.Id);
        }

        public Task<User?> FindByIdAsync(Id<User> id, CancellationToken cancellationToken = default)
            => Task.FromResult(_users.TryGetValue(id, out var user) ? user : null);

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
