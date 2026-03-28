using LgymApi.DataSeeder.Seeders;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.DataSeeder.Tests;

[TestFixture]
public sealed class SeedStatisticsPrinterTests
{
    [Test]
    public void PrintSummary_Should_Write_Seeded_Counts()
    {
        var context = new SeedContext
        {
            AdminUser = new User { Id = Id<User>.New(), Name = "Admin" },
            TesterUser = new User { Id = Id<User>.New(), Name = "Tester" }
        };

        context.DemoUsers.Add(new User { Id = Id<User>.New(), Name = "Demo" });
        context.Exercises.Add(new Exercise { Id = Id<Exercise>.New(), Name = "Bench" });
        context.ExerciseTranslations.Add(new ExerciseTranslation { Id = Id<ExerciseTranslation>.New(), ExerciseId = context.Exercises[0].Id, Culture = "pl", Name = "Wyciskanie" });
        context.Addresses.Add(new Address { Id = Id<Address>.New(), Name = "Main" });
        context.Gyms.Add(new Gym { Id = Id<Gym>.New(), Name = "Gym" });
        context.Plans.Add(new Plan { Id = Id<Plan>.New(), Name = "Plan" });
        context.PlanDays.Add(new PlanDay { Id = Id<PlanDay>.New(), Name = "Day" });
        context.PlanDayExercises.Add(new PlanDayExercise { Id = Id<PlanDayExercise>.New(), Order = 1, Series = 3, Reps = "8-12" });
        context.Trainings.Add(new Training { Id = Id<Training>.New() });
        context.ExerciseScores.Add(new ExerciseScore { Id = Id<ExerciseScore>.New() });
        context.TrainingExerciseScores.Add(new TrainingExerciseScore { Id = Id<TrainingExerciseScore>.New() });
        context.Measurements.Add(new Measurement { Id = Id<Measurement>.New(), Unit = "cm" });
        context.MainRecords.Add(new MainRecord { Id = Id<MainRecord>.New() });
        context.EloRegistries.Add(new EloRegistry { Id = Id<EloRegistry>.New(), Elo = 1000 });
        context.AppConfigs.Add(new AppConfig { Id = Id<AppConfig>.New() });
        context.Roles.Add(new Role { Id = Id<Role>.New(), Name = "Admin" });
        context.RoleClaims.Add(new RoleClaim { Id = Id<RoleClaim>.New(), RoleId = Id<Role>.New(), ClaimType = "permission", ClaimValue = "admin.access" });
        context.TrainerInvitations.Add(new TrainerInvitation { Id = Id<TrainerInvitation>.New(), TrainerId = Id<User>.New(), TraineeId = Id<User>.New(), Code = "INV", ExpiresAt = DateTimeOffset.UtcNow });
        context.TrainerTraineeLinks.Add(new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = Id<User>.New(), TraineeId = Id<User>.New() });
        context.NotificationMessages.Add(new NotificationMessage { Id = Id<NotificationMessage>.New(), Channel = NotificationChannel.Email, Type = EmailNotificationTypes.TrainerInvitation, CorrelationId = Id<CorrelationScope>.New(), Recipient = "test@lgym.app", PayloadJson = "{}" });
        context.ReportTemplates.Add(new ReportTemplate { Id = Id<ReportTemplate>.New(), TrainerId = Id<User>.New(), Name = "Report" });
        context.ReportTemplateFields.Add(new ReportTemplateField { Id = Id<ReportTemplateField>.New(), TemplateId = context.ReportTemplates[0].Id, Key = "k", Label = "l", Type = ReportFieldType.Text, Order = 1 });
        context.ReportRequests.Add(new ReportRequest { Id = Id<ReportRequest>.New(), TrainerId = Id<User>.New(), TraineeId = Id<User>.New(), TemplateId = context.ReportTemplates[0].Id });
        context.ReportSubmissions.Add(new ReportSubmission { Id = Id<ReportSubmission>.New(), ReportRequestId = context.ReportRequests[0].Id, TraineeId = Id<User>.New(), PayloadJson = "{}" });
        context.SupplementPlans.Add(new SupplementPlan { Id = Id<SupplementPlan>.New(), TrainerId = Id<User>.New(), TraineeId = Id<User>.New(), Name = "Plan", IsActive = true });
        context.SupplementPlanItems.Add(new SupplementPlanItem { Id = Id<SupplementPlanItem>.New(), PlanId = context.SupplementPlans[0].Id, SupplementName = "Whey", Dosage = "30g", TimeOfDay = TimeSpan.Zero, Order = 1 });
        context.SupplementIntakeLogs.Add(new SupplementIntakeLog { Id = Id<SupplementIntakeLog>.New(), TraineeId = Id<User>.New(), PlanItemId = context.SupplementPlanItems[0].Id, IntakeDate = DateOnly.FromDateTime(DateTime.UtcNow), TakenAt = DateTimeOffset.UtcNow });

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
            Assert.That(output, Does.Contain("Notification messages: 1"));
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
