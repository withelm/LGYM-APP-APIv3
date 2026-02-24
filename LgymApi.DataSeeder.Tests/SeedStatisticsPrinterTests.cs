using LgymApi.DataSeeder.Seeders;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;

namespace LgymApi.DataSeeder.Tests;

[TestFixture]
public sealed class SeedStatisticsPrinterTests
{
    [Test]
    public void PrintSummary_Should_Write_Seeded_Counts()
    {
        var context = new SeedContext
        {
            AdminUser = new User { Id = Guid.NewGuid(), Name = "Admin" },
            TesterUser = new User { Id = Guid.NewGuid(), Name = "Tester" }
        };

        context.DemoUsers.Add(new User { Id = Guid.NewGuid(), Name = "Demo" });
        context.Exercises.Add(new Exercise { Id = Guid.NewGuid(), Name = "Bench" });
        context.ExerciseTranslations.Add(new ExerciseTranslation { Id = Guid.NewGuid(), ExerciseId = context.Exercises[0].Id, Culture = "pl", Name = "Wyciskanie" });
        context.Addresses.Add(new Address { Id = Guid.NewGuid(), Name = "Main" });
        context.Gyms.Add(new Gym { Id = Guid.NewGuid(), Name = "Gym" });
        context.Plans.Add(new Plan { Id = Guid.NewGuid(), Name = "Plan" });
        context.PlanDays.Add(new PlanDay { Id = Guid.NewGuid(), Name = "Day" });
        context.PlanDayExercises.Add(new PlanDayExercise { Id = Guid.NewGuid(), Order = 1, Series = 3, Reps = "8-12" });
        context.Trainings.Add(new Training { Id = Guid.NewGuid() });
        context.ExerciseScores.Add(new ExerciseScore { Id = Guid.NewGuid() });
        context.TrainingExerciseScores.Add(new TrainingExerciseScore { Id = Guid.NewGuid() });
        context.Measurements.Add(new Measurement { Id = Guid.NewGuid(), Unit = "cm" });
        context.MainRecords.Add(new MainRecord { Id = Guid.NewGuid() });
        context.EloRegistries.Add(new EloRegistry { Id = Guid.NewGuid(), Elo = 1000 });
        context.AppConfigs.Add(new AppConfig { Id = Guid.NewGuid() });
        context.Roles.Add(new Role { Id = Guid.NewGuid(), Name = "Admin" });
        context.RoleClaims.Add(new RoleClaim { Id = Guid.NewGuid(), RoleId = Guid.NewGuid(), ClaimType = "permission", ClaimValue = "admin.access" });
        context.TrainerInvitations.Add(new TrainerInvitation { Id = Guid.NewGuid(), TrainerId = Guid.NewGuid(), TraineeId = Guid.NewGuid(), Code = "INV", ExpiresAt = DateTimeOffset.UtcNow });
        context.TrainerTraineeLinks.Add(new TrainerTraineeLink { Id = Guid.NewGuid(), TrainerId = Guid.NewGuid(), TraineeId = Guid.NewGuid() });
        context.EmailNotificationLogs.Add(new EmailNotificationLog { Id = Guid.NewGuid(), Type = "Invite", CorrelationId = Guid.NewGuid(), RecipientEmail = "test@lgym.app", PayloadJson = "{}" });
        context.ReportTemplates.Add(new ReportTemplate { Id = Guid.NewGuid(), TrainerId = Guid.NewGuid(), Name = "Report" });
        context.ReportTemplateFields.Add(new ReportTemplateField { Id = Guid.NewGuid(), TemplateId = context.ReportTemplates[0].Id, Key = "k", Label = "l", Type = ReportFieldType.Text, Order = 1 });
        context.ReportRequests.Add(new ReportRequest { Id = Guid.NewGuid(), TrainerId = Guid.NewGuid(), TraineeId = Guid.NewGuid(), TemplateId = context.ReportTemplates[0].Id });
        context.ReportSubmissions.Add(new ReportSubmission { Id = Guid.NewGuid(), ReportRequestId = context.ReportRequests[0].Id, TraineeId = Guid.NewGuid(), PayloadJson = "{}" });
        context.SupplementPlans.Add(new SupplementPlan { Id = Guid.NewGuid(), TrainerId = Guid.NewGuid(), TraineeId = Guid.NewGuid(), Name = "Plan", IsActive = true });
        context.SupplementPlanItems.Add(new SupplementPlanItem { Id = Guid.NewGuid(), PlanId = context.SupplementPlans[0].Id, SupplementName = "Whey", Dosage = "30g", TimeOfDay = TimeSpan.Zero, Order = 1 });
        context.SupplementIntakeLogs.Add(new SupplementIntakeLog { Id = Guid.NewGuid(), TraineeId = Guid.NewGuid(), PlanItemId = context.SupplementPlanItems[0].Id, IntakeDate = DateOnly.FromDateTime(DateTime.UtcNow), TakenAt = DateTimeOffset.UtcNow });

        using var writer = new StringWriter();
        var original = Console.Out;
        Console.SetOut(writer);

        try
        {
            SeedStatisticsPrinter.PrintSummary(context);
        }
        finally
        {
            Console.SetOut(original);
        }

        var output = writer.ToString();
        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("Users: 3"));
            Assert.That(output, Does.Contain("Exercises: 1"));
            Assert.That(output, Does.Contain("Exercise translations: 1"));
            Assert.That(output, Does.Contain("Addresses: 1"));
            Assert.That(output, Does.Contain("Gyms: 1"));
            Assert.That(output, Does.Contain("Plans: 1"));
            Assert.That(output, Does.Contain("Plan days: 1"));
            Assert.That(output, Does.Contain("Plan day exercises: 1"));
            Assert.That(output, Does.Contain("Trainings: 1"));
            Assert.That(output, Does.Contain("Exercise scores: 1"));
            Assert.That(output, Does.Contain("Training exercise scores: 1"));
            Assert.That(output, Does.Contain("Measurements: 1"));
            Assert.That(output, Does.Contain("Main records: 1"));
            Assert.That(output, Does.Contain("Elo entries: 1"));
            Assert.That(output, Does.Contain("App configs: 1"));
            Assert.That(output, Does.Contain("Roles: 1"));
            Assert.That(output, Does.Contain("Role claims: 1"));
            Assert.That(output, Does.Contain("Trainer invitations: 1"));
            Assert.That(output, Does.Contain("Trainer trainee links: 1"));
            Assert.That(output, Does.Contain("Email notification logs: 1"));
            Assert.That(output, Does.Contain("Report templates: 1"));
            Assert.That(output, Does.Contain("Report template fields: 1"));
            Assert.That(output, Does.Contain("Report requests: 1"));
            Assert.That(output, Does.Contain("Report submissions: 1"));
            Assert.That(output, Does.Contain("Supplement plans: 1"));
            Assert.That(output, Does.Contain("Supplement plan items: 1"));
            Assert.That(output, Does.Contain("Supplement intake logs: 1"));
        });
    }
}
