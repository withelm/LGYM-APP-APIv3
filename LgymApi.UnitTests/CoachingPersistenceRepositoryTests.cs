using LgymApi.Application.Coaching.Persistence;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories.Coaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class CoachingPersistenceRepositoryTests
{
    [Test]
    public async Task FactReader_ReturnsCompleteUnpagedInvitationAndDashboardFactsWithoutTracking()
    {
        await using var database = CreateDbContext();
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var linkedAt = DateTimeOffset.UtcNow.AddDays(-4);
        var invitationCreatedAt = DateTimeOffset.UtcNow.AddDays(-1);
        var olderInvitation = CreateInvitation(
            trainerId,
            traineeId,
            invitationCreatedAt,
            ParseInvitationId("00000000-0000-0000-0000-000000000001"));
        var latestInvitation = CreateInvitation(
            trainerId,
            traineeId,
            invitationCreatedAt,
            ParseInvitationId("00000000-0000-0000-0000-000000000002"));
        var emailInvitation = CreateInvitation(trainerId, null, DateTimeOffset.UtcNow);
        database.TrainerTraineeLinks.Add(new TrainerTraineeLink
        {
            Id = Id<TrainerTraineeLink>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId,
            CreatedAt = linkedAt,
            UpdatedAt = linkedAt
        });
        database.TrainerInvitations.AddRange(olderInvitation, latestInvitation, emailInvitation);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();
        var reader = new CoachingFactReader(database, CreateMapper());

        var invitationFacts = await reader.GetInvitationFactsAsync(trainerId);
        var dashboardFacts = await reader.GetDashboardFactsAsync(trainerId);

        Assert.That(invitationFacts, Has.Exactly(3).Items);
        Assert.That((invitationFacts).Any(fact => fact.Id == emailInvitation.Id && !fact.TraineeId.HasValue), Is.True);
        Assert.That(dashboardFacts, Has.Exactly(1).Items);
        Assert.That(dashboardFacts[0].TraineeId, Is.EqualTo(traineeId));
        Assert.That(dashboardFacts[0].ActiveLink!.CreatedAt, Is.EqualTo(linkedAt));
        Assert.That(dashboardFacts[0].LatestInvitation!.Id, Is.EqualTo(latestInvitation.Id));
        Assert.That(database.ChangeTracker.Entries(), Is.Empty);
    }

    [Test]
    public async Task FocusedPersistenceRepositories_StageWritesAndReturnImmutableFacts()
    {
        await using var database = CreateDbContext();
        var mapper = CreateMapper();
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var invitationId = Id<TrainerInvitation>.New();
        var linkId = Id<TrainerTraineeLink>.New();
        var noteId = Id<TraineeNote>.New();
        var historyId = Id<TraineeNoteHistory>.New();
        var now = DateTimeOffset.UtcNow;
        var invitationRepository = new CoachingInvitationPersistenceRepository(database, mapper);
        var linkRepository = new CoachingActiveLinkPersistenceRepository(database, mapper);
        var noteRepository = new CoachingTraineeNotePersistenceRepository(database, mapper);

        await invitationRepository.AddAsync(new CoachingInvitationWriteModel(
            invitationId, trainerId, "trainee@example.test", traineeId, "code", TrainerInvitationStatus.Pending, now.AddDays(3), now, null));
        await linkRepository.AddAsync(new CoachingActiveLinkWriteModel(linkId, trainerId, traineeId));
        await noteRepository.AddNoteAsync(new CoachingTraineeNoteWriteModel(
            noteId, trainerId, traineeId, "Title", "Content", true, true, trainerId, now));
        await noteRepository.AddHistoryEntryAsync(new CoachingTraineeNoteHistoryWriteModel(
            historyId, noteId, trainerId, now, null, "Content", false, true, "Created"));

        Assert.That(database.ChangeTracker.Entries().Where(entry => entry.State == EntityState.Added), Has.Exactly(5).Items);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();

        Assert.That((await invitationRepository.FindByIdAsync(invitationId))!.Code, Is.EqualTo("code"));
        Assert.That((await linkRepository.FindByTrainerAndTraineeAsync(trainerId, traineeId))!.Id, Is.EqualTo(linkId));
        var persistedNote = (await noteRepository.FindNoteByIdAsync(noteId))!;
        Assert.That(persistedNote.Content, Is.EqualTo("Content"));
        Assert.That(((await noteRepository.GetNoteHistoryAsync(noteId))).Where(entry => entry.Id == historyId), Has.Exactly(1).Items);
        Assert.That(database.ChangeTracker.Entries(), Is.Empty);

        await noteRepository.UpdateNoteAsync(new CoachingTraineeNoteWriteModel(
            noteId, trainerId, traineeId, null, "Updated", false, false, trainerId, now.AddMinutes(1), true, persistedNote.UpdatedAt));
        Assert.That((database.ChangeTracker.Entries<TraineeNote>()).Where(entry => entry.State == EntityState.Modified), Has.Exactly(1).Items);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();
        var deletedNote = await database.TraineeNotes.IgnoreQueryFilters().SingleAsync(note => note.Id == noteId);
        Assert.That(deletedNote.Content, Is.EqualTo("Updated"));
        Assert.That(deletedNote.VisibleToTrainee, Is.False);
        Assert.That(deletedNote.IsDeleted, Is.True);
    }

    [Test]
    public async Task InvitationResponseUpdate_StagesExpiredStateWithoutBindingTrainee()
    {
        await using var database = CreateDbContext();
        var trainerId = Id<User>.New();
        var invitation = CreateInvitation(trainerId, null, DateTimeOffset.UtcNow.AddDays(-1));
        database.TrainerInvitations.Add(invitation);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();
        var repository = new CoachingInvitationPersistenceRepository(database, CreateMapper());
        var respondedAt = DateTimeOffset.UtcNow;

        await repository.UpdateResponseAsync(new CoachingInvitationResponseUpdateModel(
            invitation.Id,
            null,
            TrainerInvitationStatus.Expired,
            respondedAt));

        var entry = database.ChangeTracker.Entries<TrainerInvitation>().Single();
        Assert.That(entry.Property(candidate => candidate.Status).IsModified, Is.True);
        Assert.That(entry.Property(candidate => candidate.RespondedAt).IsModified, Is.True);
        Assert.That(entry.Property(candidate => candidate.TraineeId).IsModified, Is.False);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();

        var persisted = await database.TrainerInvitations.SingleAsync(candidate => candidate.Id == invitation.Id);
        Assert.That(persisted.Status, Is.EqualTo(TrainerInvitationStatus.Expired));
        Assert.That(persisted.RespondedAt, Is.EqualTo(respondedAt));
        Assert.That(persisted.TraineeId, Is.Null);
    }

    [Test]
    public void FocusedPersistenceContracts_AreRegisteredOnceAndMapEveryFactField()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase($"coaching-persistence-di-{Id<CoachingPersistenceRepositoryTests>.New().ToString().Replace("-", string.Empty, StringComparison.Ordinal)}"));
        services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);
        services.AddCoachingInfrastructure();
        var contracts = new[]
        {
            typeof(ICoachingInvitationPersistence),
            typeof(ICoachingActiveLinkPersistence),
            typeof(ICoachingFactReader),
            typeof(ICoachingTraineeNotePersistence)
        };

        foreach (var contract in contracts)
        {
            Assert.That(services.Count(descriptor => descriptor.ServiceType == contract), Is.EqualTo(1));
        }

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
        var invitation = CreateInvitation(Id<User>.New(), Id<User>.New(), DateTimeOffset.UtcNow);
        var activeLink = new CoachingActiveLinkFact(
            Id<TrainerTraineeLink>.New(),
            invitation.TrainerId,
            invitation.TraineeId!.Value,
            invitation.CreatedAt,
            invitation.UpdatedAt);

        Assert.That(scope.ServiceProvider.GetRequiredService<ICoachingInvitationPersistence>(), Is.TypeOf<CoachingInvitationPersistenceRepository>());
        Assert.That(scope.ServiceProvider.GetRequiredService<ICoachingActiveLinkPersistence>(), Is.TypeOf<CoachingActiveLinkPersistenceRepository>());
        Assert.That(scope.ServiceProvider.GetRequiredService<ICoachingFactReader>(), Is.TypeOf<CoachingFactReader>());
        Assert.That(scope.ServiceProvider.GetRequiredService<ICoachingTraineeNotePersistence>(), Is.TypeOf<CoachingTraineeNotePersistenceRepository>());
        Assert.That(mapper.Map<TrainerInvitation, CoachingInvitationFact>(invitation, mapper.CreateContext()), Is.EqualTo(new CoachingInvitationFact(
            invitation.Id,
            invitation.TrainerId,
            invitation.InviteeEmail,
            invitation.TraineeId,
            invitation.Code,
            invitation.Status,
            invitation.ExpiresAt,
            invitation.RespondedAt,
            invitation.CreatedAt,
            invitation.UpdatedAt)).UsingPropertiesComparer());
        Assert.That(mapper.Map<CoachingDashboardSource, CoachingDashboardFact>(
            new CoachingDashboardSource(activeLink.TraineeId, activeLink, null),
            mapper.CreateContext()), Is.EqualTo(new CoachingDashboardFact(activeLink.TraineeId, activeLink, null)).UsingPropertiesComparer());
    }

    private static AppDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"coaching-persistence-{Id<CoachingPersistenceRepositoryTests>.New().ToString().Replace("-", string.Empty, StringComparison.Ordinal)}")
            .Options);

    private static IMapper CreateMapper()
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);
        return services.BuildServiceProvider().GetRequiredService<IMapper>();
    }

    private static TrainerInvitation CreateInvitation(
        Id<User> trainerId,
        Id<User>? traineeId,
        DateTimeOffset createdAt,
        Id<TrainerInvitation>? invitationId = null)
        => new()
        {
            Id = invitationId ?? Id<TrainerInvitation>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId,
            InviteeEmail = "trainee@example.test",
            Code = Id<TrainerInvitation>.New().ToString().Replace("-", string.Empty, StringComparison.Ordinal),
            Status = TrainerInvitationStatus.Pending,
            ExpiresAt = createdAt.AddDays(7),
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

    private static Id<TrainerInvitation> ParseInvitationId(string value) =>
        Id<TrainerInvitation>.TryParse(value, out var invitationId)
            ? invitationId
            : throw new ArgumentException("Invitation ID must be valid.", nameof(value));
}
