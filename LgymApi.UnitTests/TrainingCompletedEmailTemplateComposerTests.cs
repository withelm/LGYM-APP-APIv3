using System.Globalization;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.Domain.Enums;
using LgymApi.Infrastructure.Options;
using LgymApi.Infrastructure.Services;
using LgymApi.Resources;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class TrainingCompletedEmailTemplateComposerTests
{
    private string _templateRootPath = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _templateRootPath = Path.Combine(Path.GetTempPath(), $"lgym-training-email-templates-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_templateRootPath, "TrainingCompleted"));
        File.WriteAllText(
            Path.Combine(_templateRootPath, "TrainingCompleted", "en.email"),
            "Subject: Training completed - {{PlanDayName}}\n---\nPlan: {{PlanDayName}}\nDate: {{TrainingDate}}\n\n{{TrainingTable}}");
        File.WriteAllText(
            Path.Combine(_templateRootPath, "TrainingCompleted", "pl.email"),
            "Subject: Trening zakonczony - {{PlanDayName}}\n---\nPlan: {{PlanDayName}}\nData: {{TrainingDate}}\n\n{{TrainingTable}}");
    }

    [TearDown]
    public void TearDown()
    {
        if (!string.IsNullOrWhiteSpace(_templateRootPath) && Directory.Exists(_templateRootPath))
        {
            Directory.Delete(_templateRootPath, recursive: true);
        }
    }

    [Test]
    public void ComposeTrainingCompleted_ShouldRenderTableWithNames()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        var composer = CreateComposer();
        var payload = new TrainingCompletedEmailPayload
        {
            UserId = Guid.NewGuid(),
            TrainingId = Guid.NewGuid(),
            RecipientEmail = "user@example.com",
            CultureName = "en-US",
            TimeZoneId = "Europe/Warsaw",
            PlanDayName = "Upper Body A",
            TrainingDate = DateTimeOffset.Parse("2026-03-01T10:00:00+00:00"),
            Exercises = new List<TrainingExerciseSummary>
            {
                new()
                {
                    ExerciseName = "Bench Press",
                    Series = 1,
                    Reps = 8,
                    Weight = 80,
                    Unit = WeightUnits.Kilograms
                },
                new()
                {
                    ExerciseName = "Bench Press",
                    Series = 2,
                    Reps = 6,
                    Weight = 85,
                    Unit = WeightUnits.Kilograms
                }
            }
        };

        var message = composer.ComposeTrainingCompleted(payload);

        Assert.Multiple(() =>
        {
            Assert.That(message.To, Is.EqualTo("user@example.com"));
            Assert.That(message.Subject, Is.EqualTo("Training completed - Upper Body A"));
            Assert.That(message.IsHtml, Is.True);
            Assert.That(message.Body, Does.Contain("<table"));
            Assert.That(message.Body, Does.Contain("Bench Press"));
            Assert.That(message.Body, Does.Contain("Date: 2026-03-01 11:00"));
            Assert.That(message.Body, Does.Contain($"{Emails.TrainingSeriesLabel} #1"));
            Assert.That(message.Body, Does.Contain($"{Emails.TrainingSeriesLabel} #2"));
            Assert.That(message.Body, Does.Contain(">8<"));
            Assert.That(message.Body, Does.Contain(">80<"));
        });
    }

    [Test]
    public void ComposeTrainingCompleted_ShouldFallbackToDefaultLanguage_WhenTemplateMissing()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        var composer = CreateComposer();
        var payload = new TrainingCompletedEmailPayload
        {
            UserId = Guid.NewGuid(),
            TrainingId = Guid.NewGuid(),
            RecipientEmail = "user@example.com",
            CultureName = "de-DE",
            TimeZoneId = "Europe/Warsaw",
            PlanDayName = "Lower Body B",
            TrainingDate = DateTimeOffset.Parse("2026-03-01T10:00:00+00:00"),
            Exercises = new List<TrainingExerciseSummary>()
        };

        var message = composer.ComposeTrainingCompleted(payload);

        Assert.Multiple(() =>
        {
            Assert.That(message.Subject, Is.EqualTo("Training completed - Lower Body B"));
            Assert.That(message.IsHtml, Is.True);
            Assert.That(message.Body, Does.Contain(Emails.TrainingNoExercises));
        });
    }

    [Test]
    public void ComposeTrainingCompleted_ShouldPreserveExerciseGroupOrder_NotAlphabetical()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        var composer = CreateComposer();
        var payload = new TrainingCompletedEmailPayload
        {
            UserId = Guid.NewGuid(),
            TrainingId = Guid.NewGuid(),
            RecipientEmail = "user@example.com",
            CultureName = "en-US",
            TimeZoneId = "Europe/Warsaw",
            PlanDayName = "Full Body",
            TrainingDate = DateTimeOffset.Parse("2026-03-01T10:00:00+00:00"),
            Exercises = new List<TrainingExerciseSummary>
            {
                // Order in list: Squats, Bench Press, Deadlifts
                // Alphabetical would be: Bench Press, Deadlifts, Squats
                new()
                {
                    ExerciseName = "Squats",
                    Series = 1,
                    Reps = 10,
                    Weight = 100,
                    Unit = WeightUnits.Kilograms
                },
                new()
                {
                    ExerciseName = "Bench Press",
                    Series = 1,
                    Reps = 8,
                    Weight = 80,
                    Unit = WeightUnits.Kilograms
                },
                new()
                {
                    ExerciseName = "Deadlifts",
                    Series = 1,
                    Reps = 5,
                    Weight = 120,
                    Unit = WeightUnits.Kilograms
                }
            }
        };

        var message = composer.ComposeTrainingCompleted(payload);

        // Find the positions of exercise names in the HTML body
        var squatsPos = message.Body.IndexOf(">Squats<", StringComparison.Ordinal);
        var benchPos = message.Body.IndexOf(">Bench Press<", StringComparison.Ordinal);
        var deadliftsPos = message.Body.IndexOf(">Deadlifts<", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(squatsPos, Is.GreaterThan(-1), "Squats not found in body");
            Assert.That(benchPos, Is.GreaterThan(-1), "Bench Press not found in body");
            Assert.That(deadliftsPos, Is.GreaterThan(-1), "Deadlifts not found in body");
            // Verify the order: Squats should appear before Bench Press, which should appear before Deadlifts
            Assert.That(squatsPos, Is.LessThan(benchPos), "Squats should appear before Bench Press (payload order, not alphabetical)");
            Assert.That(benchPos, Is.LessThan(deadliftsPos), "Bench Press should appear before Deadlifts (payload order, not alphabetical)");
        });
    }

    [Test]
    public void ComposeTrainingCompleted_ShouldRenderLocalizedUnitDisplayName()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        var composer = CreateComposer();
        var payload = new TrainingCompletedEmailPayload
        {
            UserId = Guid.NewGuid(),
            TrainingId = Guid.NewGuid(),
            RecipientEmail = "user@example.com",
            CultureName = "en-US",
            TimeZoneId = "Europe/Warsaw",
            PlanDayName = "Testing Units",
            TrainingDate = DateTimeOffset.Parse("2026-03-01T10:00:00+00:00"),
            Exercises = new List<TrainingExerciseSummary>
            {
                new()
                {
                    ExerciseName = "Deadlifts",
                    Series = 1,
                    Reps = 5,
                    Weight = 100,
                    Unit = WeightUnits.Kilograms
                },
                new()
                {
                    ExerciseName = "Bench Press",
                    Series = 1,
                    Reps = 8,
                    Weight = 185,
                    Unit = WeightUnits.Pounds
                }
            }
        };

        var message = composer.ComposeTrainingCompleted(payload);

        Assert.Multiple(() =>
        {
            Assert.That(message.Body, Does.Contain(">kg<"), "Should render localized name for Kilograms");
            Assert.That(message.Body, Does.Contain(">lbs<"), "Should render localized name for Pounds");
            Assert.That(message.Body, Does.Not.Contain(">Kilograms<"), "Should not render enum name for Kilograms");
            Assert.That(message.Body, Does.Not.Contain(">Pounds<"), "Should not render enum name for Pounds");
        });
    }
    private TrainingCompletedEmailTemplateComposer CreateComposer()
    {
        return new TrainingCompletedEmailTemplateComposer(new EmailOptions
        {
            InvitationBaseUrl = "https://app.example.com/invitations",
            TemplateRootPath = _templateRootPath,
            DefaultCulture = CultureInfo.GetCultureInfo("en-US")
        });
    }
}
