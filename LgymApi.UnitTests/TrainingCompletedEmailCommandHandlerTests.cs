using FluentAssertions;
using LgymApi.Application.Options;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Actions;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class TrainingCompletedEmailCommandHandlerTests
{
    [Test]
    public async Task ExecuteAsync_WhenScoreExists_PreservesTypedExerciseId()
    {
        var exerciseId = Id<Exercise>.New();
        var payload = await ExecuteAsyncWithScoresAsync(CreateExerciseScore(exerciseId));

        payload.Exercises.Should().ContainSingle();
        payload.Exercises.Single().ExerciseId.Should().Be(exerciseId);
    }

    [Test]
    public async Task ExecuteAsync_WhenScoreIsMissing_UsesEmptyExerciseId()
    {
        var payload = await ExecuteAsyncWithScoresAsync();

        payload.Exercises.Should().ContainSingle();
        payload.Exercises.Single().ExerciseId.Should().Be(Id<Exercise>.Empty);
    }

    private static async Task<TrainingCompletedEmailPayload> ExecuteAsyncWithScoresAsync(params ExerciseScore[] exerciseScores)
    {
        var userId = Id<User>.New();
        var trainingId = Id<Training>.New();
        var exerciseScoreId = exerciseScores.FirstOrDefault()?.Id ?? Id<ExerciseScore>.New();
        var userRepository = Substitute.For<IUserRepository>();
        var trainingRepository = Substitute.For<ITrainingRepository>();
        var trainingExerciseScoreRepository = Substitute.For<ITrainingExerciseScoreRepository>();
        var exerciseScoreRepository = Substitute.For<IExerciseScoreRepository>();
        var subscriptionRepository = Substitute.For<IEmailNotificationSubscriptionRepository>();
        var emailScheduler = Substitute.For<IEmailScheduler<TrainingCompletedEmailPayload>>();
        TrainingCompletedEmailPayload? scheduledPayload = null;

        userRepository.FindByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = userId, Email = "athlete@example.com" });
        subscriptionRepository.IsSubscribedAsync(userId, EmailNotificationTypes.TrainingCompleted.Value, Arg.Any<CancellationToken>())
            .Returns(true);
        trainingExerciseScoreRepository.GetByTrainingIdsAsync(Arg.Any<List<Id<Training>>>(), Arg.Any<CancellationToken>())
            .Returns([new TrainingExerciseScore { TrainingId = trainingId, ExerciseScoreId = exerciseScoreId }]);
        exerciseScoreRepository.GetByIdsAsync(Arg.Any<List<Id<ExerciseScore>>>(), Arg.Any<CancellationToken>())
            .Returns(exerciseScores.ToList());
        trainingRepository.GetByIdAsync(trainingId, Arg.Any<CancellationToken>())
            .Returns(new Training
            {
                Id = trainingId,
                CreatedAt = DateTimeOffset.UtcNow,
                PlanDay = new PlanDay { Name = "Upper Body" }
            });
        emailScheduler.ScheduleAsync(Arg.Do<TrainingCompletedEmailPayload>(payload => scheduledPayload = payload), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = new TrainingCompletedEmailCommandHandler(
            userRepository,
            trainingRepository,
            trainingExerciseScoreRepository,
            exerciseScoreRepository,
            subscriptionRepository,
            emailScheduler,
            Substitute.For<ILogger<TrainingCompletedEmailCommandHandler>>(),
            new AppDefaultsOptions());

        await handler.ExecuteAsync(new TrainingCompletedCommand { UserId = userId, TrainingId = trainingId });

        scheduledPayload.Should().NotBeNull();
        return scheduledPayload!;
    }

    private static ExerciseScore CreateExerciseScore(Id<Exercise> exerciseId)
    {
        return new ExerciseScore
        {
            Id = Id<ExerciseScore>.New(),
            ExerciseId = exerciseId,
            Exercise = new Exercise { Id = exerciseId, Name = "Bench Press" },
            Series = 1,
            Reps = 8,
            Weight = new Weight(80, WeightUnits.Kilograms)
        };
    }
}
