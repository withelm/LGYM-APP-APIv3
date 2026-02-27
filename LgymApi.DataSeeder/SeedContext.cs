using LgymApi.Domain.Entities;

namespace LgymApi.DataSeeder;

public sealed class SeedContext
{
    public bool SeedDemoData { get; set; }
    public User? AdminUser { get; set; }
    public User? TesterUser { get; set; }
    public List<User> DemoUsers { get; } = new();
    public List<Exercise> Exercises { get; } = new();
    public List<ExerciseTranslation> ExerciseTranslations { get; } = new();
    public List<Address> Addresses { get; } = new();
    public List<Gym> Gyms { get; } = new();
    public List<Plan> Plans { get; } = new();
    public List<PlanDay> PlanDays { get; } = new();
    public List<PlanDayExercise> PlanDayExercises { get; } = new();
    public List<Training> Trainings { get; } = new();
    public List<ExerciseScore> ExerciseScores { get; } = new();
    public List<TrainingExerciseScore> TrainingExerciseScores { get; } = new();
    public List<Measurement> Measurements { get; } = new();
    public List<MainRecord> MainRecords { get; } = new();
    public List<EloRegistry> EloRegistries { get; } = new();
    public List<AppConfig> AppConfigs { get; } = new();
    public List<Role> Roles { get; } = new();
    public List<RoleClaim> RoleClaims { get; } = new();
    public List<TrainerInvitation> TrainerInvitations { get; } = new();
    public List<TrainerTraineeLink> TrainerTraineeLinks { get; } = new();
    public List<EmailNotificationLog> EmailNotificationLogs { get; } = new();
    public List<EmailNotificationSubscription> EmailNotificationSubscriptions { get; } = new();
    public List<ReportTemplate> ReportTemplates { get; } = new();
    public List<ReportTemplateField> ReportTemplateFields { get; } = new();
    public List<ReportRequest> ReportRequests { get; } = new();
    public List<ReportSubmission> ReportSubmissions { get; } = new();
    public List<SupplementPlan> SupplementPlans { get; } = new();
    public List<SupplementPlanItem> SupplementPlanItems { get; } = new();
    public List<SupplementIntakeLog> SupplementIntakeLogs { get; } = new();
}
