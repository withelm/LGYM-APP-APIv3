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
    public void RegisteredLedgerDirections_PreserveEveryStateFieldAndActorWithoutEntityAudit(bool bound)
    {
        using var provider = CompositionRootTestHost.Create().BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        var exerciseId = Id<Exercise>.New();
        var objectKey = $"exercises/{exerciseId}/{new string('b', 64)}/animation.gif";
        var source = new ExerciseMediaUpload
        {
            Id = Id<ExerciseMediaUpload>.New(), ActorId = Id<User>.New(),
            TargetExerciseId = bound ? exerciseId : null,
            ContentType = "image/gif", SourceSha256 = new string('a', 64),
            PublishedSha256 = bound ? new string('b', 64) : null, Extension = bound ? "gif" : null,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24), Status = bound ? "bound" : "rejected", Revision = 17,
            OperationId = Id<ExerciseMediaOperationReference>.New(), OperationHash = new string('c', 64),
            PreviousImage = bound ? "https://legacy.owner.test/retained.gif" : null,
            ResultImage = bound ? "https://media.example.test/" + objectKey : null,
            ObjectKey = bound ? objectKey : null, RejectionStatusCode = bound ? null : 422,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1), UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-1), IsDeleted = true
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

        session.Status = "changed-projection-only";
        session.ObjectKey = "changed-projection-only";
        source.Status.Should().Be(bound ? "bound" : "rejected");
        source.ObjectKey.Should().Be(bound ? objectKey : null);
        roundtrip.Status.Should().Be(source.Status);
        roundtrip.ObjectKey.Should().Be(source.ObjectKey);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RegisteredObjectFenceDirections_PreservePermanentRetirementAndCleanupWithoutEntityAudit(bool retired)
    {
        using var provider = CompositionRootTestHost.Create().BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        var exerciseId = Id<Exercise>.New();
        var objectKey = $"exercises/{exerciseId}/{new string('b', 64)}/animation.gif";
        var source = new ExerciseMediaObjectFence
        {
            Id = Id<ExerciseMediaObjectFence>.New(), ExerciseId = exerciseId, ObjectKey = objectKey,
            PublicUrl = "https://media.example.test/" + objectKey, Revision = 19,
            IsRetired = retired, LastCleanupAt = retired ? DateTimeOffset.UtcNow.AddMinutes(-2) : null,
            LastDeleteSucceeded = retired,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1), UpdatedAt = DateTimeOffset.UtcNow, IsDeleted = true
        };

        var state = mapper.Map<ExerciseMediaObjectFence, ExerciseMediaObjectState>(source);
        state.Should().BeEquivalentTo(source, options => options.Excluding(x => x.CreatedAt).Excluding(x => x.UpdatedAt).Excluding(x => x.IsDeleted));
        var roundtrip = mapper.Map<ExerciseMediaObjectState, ExerciseMediaObjectFence>(state);
        roundtrip.Should().NotBeSameAs(source);
        roundtrip.Should().BeEquivalentTo(source, options => options.Excluding(x => x.CreatedAt).Excluding(x => x.UpdatedAt).Excluding(x => x.IsDeleted));
        roundtrip.CreatedAt.Should().Be(default);
        roundtrip.UpdatedAt.Should().Be(default);
        roundtrip.IsDeleted.Should().BeFalse();
        state.ObjectKey = "changed-projection-only";
        state.IsRetired = !retired;
        source.ObjectKey.Should().Be(objectKey);
        source.IsRetired.Should().Be(retired);
        roundtrip.ObjectKey.Should().Be(objectKey);
        roundtrip.IsRetired.Should().Be(retired);
    }
}
