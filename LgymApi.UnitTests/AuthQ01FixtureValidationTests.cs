using LgymApi.TestUtils;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class AuthQ01FixtureValidationTests
{
    [TestCaseSource(nameof(InvalidFixtures))]
    public void Validate_WhenScenarioContractIsBroken_RejectsFixture(Func<AuthQ01Fixture, AuthQ01Fixture> invalidate)
    {
        var canonical = AuthQ01FixtureLoader.Load();
        var invalid = invalidate(canonical);

        Assert.Throws<InvalidOperationException>(() => invalid.Validate());
    }

    [Test]
    public void Validate_WhenDietHasNoEndDate_AcceptsCalendarOnlyContract()
    {
        var canonical = AuthQ01FixtureLoader.Load();
        var openEnded = canonical with
        {
            Diets = ChangeFirst(canonical.Diets, diet => diet with { EndDate = null })
        };

        Assert.DoesNotThrow(() => openEnded.Validate());
    }

    [Test]
    public void Validate_WhenPhotoEncodingIsInvalid_PreservesFormatFailure()
    {
        var invalid = AuthQ01FixtureLoader.Load() with { SyntheticPngBase64 = "not-base64!" };

        var exception = Assert.Throws<InvalidOperationException>(() => invalid.Validate());

        Assert.That(exception.InnerException, Is.TypeOf<FormatException>());
    }

    [Test]
    public void Actor_WhenAliasExists_ReturnsItsExactIdentity()
    {
        var fixture = AuthQ01FixtureLoader.Load();

        var actor = fixture.Actor("AdminTrainer");

        Assert.Multiple(() =>
        {
            Assert.That(actor.Id, Is.EqualTo("23600000-0000-0000-0000-000000000006"));
            Assert.That(actor.Email, Is.EqualTo("admin-trainer@auth-q01.invalid"));
            Assert.That(actor.Roles, Is.EqualTo(new[] { "User", "Admin", "Trainer" }));
        });
    }

    [Test]
    public void Actor_WhenAliasIsUnknown_DoesNotSubstituteAnotherActor()
    {
        var fixture = AuthQ01FixtureLoader.Load();

        Assert.Throws<InvalidOperationException>(() => fixture.Actor("Unknown"));
    }

    private static IEnumerable<TestCaseData> InvalidFixtures()
    {
        (string Name, Func<AuthQ01Fixture, AuthQ01Fixture> Invalidate)[] cases =
        [
            ("unsupported schema", f => f with { SchemaVersion = 2 }),
            ("wrong fixture identity", f => f with { FixtureId = "another-fixture" }),
            ("non-synthetic marker", f => f with { SyntheticOnly = false }),
            ("missing actor", f => f with { Actors = f.Actors.Skip(1).ToArray() }),
            ("duplicate actor ID", f => f with { Actors = ChangeFirst(f.Actors, a => a with { Id = f.Actors[1].Id }) }),
            ("invalid actor ID", f => f with { Actors = ChangeFirst(f.Actors, a => a with { Id = "invalid" }) }),
            ("empty roles", f => f with { Actors = ChangeFirst(f.Actors, a => a with { Roles = [] }) }),
            ("missing User role", f => f with { Actors = ChangeFirst(f.Actors, a => a with { Roles = ["Trainer"] }) }),
            ("unknown role", f => f with { Actors = ChangeFirst(f.Actors, a => a with { Roles = ["User", "Unknown"] }) }),
            ("unknown relationship trainer", f => f with { RelationshipScenarios = f.RelationshipScenarios with { Initial = [new("Unknown", "U")] } }),
            ("unknown relationship trainee", f => f with { RelationshipScenarios = f.RelationshipScenarios with { Initial = [new("T1", "Unknown")] } }),
            ("report at relationship start", f => f with { Timeline = f.Timeline with { StartedAt = f.Timeline.ReportAcceptedAt } }),
            ("acceptance at unlink", f => f with { Timeline = f.Timeline with { ReportAcceptedAt = f.Timeline.RelationshipEndedAt } }),
            ("persistence at unlink", f => f with { Timeline = f.Timeline with { MeasurementPersistedAt = f.Timeline.RelationshipEndedAt } }),
            ("post-unlink observation at persistence", f => f with { Timeline = f.Timeline with { PostUnlinkObservedAt = f.Timeline.MeasurementPersistedAt } }),
            ("return at post-unlink observation", f => f with { Timeline = f.Timeline with { ReturnedAt = f.Timeline.PostUnlinkObservedAt } }),
            ("reference clock at return", f => f with { Timeline = f.Timeline with { ReferenceNow = f.Timeline.ReturnedAt } }),
            ("duplicate plan ID", f => f with { Plans = [.. f.Plans, f.Plans[0]] }),
            ("duplicate report ID", f => f with { Reports = [.. f.Reports, f.Reports[0]] }),
            ("invalid plan ID", f => f with { Plans = ChangeFirst(f.Plans, p => p with { Id = "invalid" }) }),
            ("invalid report ID", f => f with { Reports = ChangeFirst(f.Reports, r => r with { Id = "invalid" }) }),
            ("unknown plan owner", f => f with { Plans = ChangeFirst(f.Plans, p => p with { Owner = "Unknown" }) }),
            ("unknown report source", f => f with { Reports = ChangeFirst(f.Reports, r => r with { SourceTrainer = "Unknown" }) }),
            ("unknown report trainee", f => f with { Reports = ChangeFirst(f.Reports, r => r with { Trainee = "Unknown" }) }),
            ("duplicate note version", f => f with { NoteVersions = [.. f.NoteVersions, f.NoteVersions[0]] }),
            ("unknown note author", f => f with { NoteVersions = ChangeFirst(f.NoteVersions, n => n with { Author = "Unknown" }) }),
            ("unknown note trainee", f => f with { NoteVersions = ChangeFirst(f.NoteVersions, n => n with { Trainee = "Unknown" }) }),
            ("empty note content", f => f with { NoteVersions = ChangeFirst(f.NoteVersions, n => n with { Content = " " }) }),
            ("unknown diet author", f => f with { Diets = ChangeFirst(f.Diets, d => d with { Author = "Unknown" }) }),
            ("unknown diet trainee", f => f with { Diets = ChangeFirst(f.Diets, d => d with { Trainee = "Unknown" }) }),
            ("diet ends before start", f => f with { Diets = ChangeFirst(f.Diets, d => d with { EndDate = d.StartDate.AddDays(-1) }) }),
            ("photo source missing", f => f with { Photos = ChangeFirst(f.Photos, p => p with { SourceReportId = "missing" }) }),
            ("share source missing", f => f with { ShareCodes = ChangeFirst(f.ShareCodes, s => s with { SourcePlanId = "missing" }) }),
            ("source report accepted after unlink", f => f with { Reports = ChangeFirst(f.Reports, r => r with { AcceptedAt = f.Timeline.RelationshipEndedAt }) }),
            ("measurement admitted after unlink", f => f with { Measurements = ChangeFirst(f.Measurements, m => m with { AcceptedAt = f.Timeline.RelationshipEndedAt }) })
        ];

        foreach (var (name, invalidate) in cases)
        {
            yield return new TestCaseData(invalidate).SetName($"AUTH-Q01 rejects {name}");
        }
    }

    private static IReadOnlyList<T> ChangeFirst<T>(IReadOnlyList<T> items, Func<T, T> change)
        => [change(items[0]), .. items.Skip(1)];
}
