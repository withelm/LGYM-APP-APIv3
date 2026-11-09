using FluentAssertions;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ExerciseMedia;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ExerciseMediaPersistenceMappingTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void RegisteredDirections_PreserveAllLedgerFieldsAndActorScopeWithoutTransportingEntityAudit(bool published)
    {
        using var provider = CompositionRootTestHost.Create().BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        var source = new ExerciseMediaUpload
        {
            Id = Id<ExerciseMediaUpload>.New(),
            ActorId = Id<User>.New(),
            TargetExerciseId = published ? Id<Exercise>.New() : null,
            ContentType = "image/gif",
            SizeBytes = 4321,
            SourceSha256 = new string('a', 64),
            PublishedSha256 = published ? new string('b', 64) : null,
            Extension = published ? "gif" : null,
            Width = 418,
            Height = 419,
            Frames = 16,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24),
            Status = published ? "bound" : "pending",
            Revision = 17,
            OperationId = published ? Id<ExerciseMediaOperationReference>.New() : null,
            OperationHash = published ? new string('c', 64) : null,
            PreviousImage = published ? "https://legacy.example.invalid/retained.gif" : null,
            ResultImage = published ? "https://exercise.example.invalid/published.gif" : null,
            PrivateObjectsCleaned = published,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            IsDeleted = true
        };

        var session = mapper.Map<ExerciseMediaUpload, ExerciseMediaSession>(source);
        session.Id.Should().Be(source.Id);
        session.ActorId.Should().Be(source.ActorId.Rebind<AccountReference>());
        ((IExerciseMediaState)session).Should().BeEquivalentTo((IExerciseMediaState)source);

        var roundtrip = mapper.Map<ExerciseMediaSession, ExerciseMediaUpload>(session);
        roundtrip.Should().NotBeSameAs(source);
        roundtrip.Id.Should().Be(source.Id);
        roundtrip.ActorId.Should().Be(source.ActorId);
        ((IExerciseMediaState)roundtrip).Should().BeEquivalentTo((IExerciseMediaState)source);
        roundtrip.CreatedAt.Should().Be(default);
        roundtrip.UpdatedAt.Should().Be(default);
        roundtrip.IsDeleted.Should().BeFalse();

        session.Status = "expired";
        source.Status.Should().Be(published ? "bound" : "pending");
        roundtrip.Status.Should().Be(source.Status);
    }
}
