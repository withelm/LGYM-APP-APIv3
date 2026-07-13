using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data.Configurations;
using LgymApi.Infrastructure.Data.Conventions;
using LgymApi.Infrastructure.Data.SeedData;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace LgymApi.Infrastructure.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanDay> PlanDays => Set<PlanDay>();
    public DbSet<PlanDayExercise> PlanDayExercises => Set<PlanDayExercise>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<ExerciseTranslation> ExerciseTranslations => Set<ExerciseTranslation>();
    public DbSet<Training> Trainings => Set<Training>();
    public DbSet<TrainingExerciseScore> TrainingExerciseScores => Set<TrainingExerciseScore>();
    public DbSet<ExerciseScore> ExerciseScores => Set<ExerciseScore>();
    public DbSet<Measurement> Measurements => Set<Measurement>();
    public DbSet<MainRecord> MainRecords => Set<MainRecord>();
    public DbSet<Gym> Gyms => Set<Gym>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<EloRegistry> EloRegistries => Set<EloRegistry>();
    public DbSet<AppConfig> AppConfigs => Set<AppConfig>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RoleClaim> RoleClaims => Set<RoleClaim>();
    public DbSet<TrainerInvitation> TrainerInvitations => Set<TrainerInvitation>();
    public DbSet<TrainerTraineeLink> TrainerTraineeLinks => Set<TrainerTraineeLink>();
    public DbSet<NotificationMessage> NotificationMessages => Set<NotificationMessage>();
    public DbSet<EmailNotificationSubscription> EmailNotificationSubscriptions => Set<EmailNotificationSubscription>();
    public DbSet<ReportTemplate> ReportTemplates => Set<ReportTemplate>();
    public DbSet<ReportTemplateField> ReportTemplateFields => Set<ReportTemplateField>();
    public DbSet<RecurringReportAssignment> RecurringReportAssignments => Set<RecurringReportAssignment>();
    public DbSet<ReportRequest> ReportRequests => Set<ReportRequest>();
    public DbSet<ReportSubmission> ReportSubmissions => Set<ReportSubmission>();
    public DbSet<SupplementPlan> SupplementPlans => Set<SupplementPlan>();
    public DbSet<SupplementPlanItem> SupplementPlanItems => Set<SupplementPlanItem>();
    public DbSet<SupplementIntakeLog> SupplementIntakeLogs => Set<SupplementIntakeLog>();
    public DbSet<DietPlan> DietPlans => Set<DietPlan>();
    public DbSet<DietMeal> DietMeals => Set<DietMeal>();
    public DbSet<DietPlanHistory> DietPlanHistories => Set<DietPlanHistory>();
    public DbSet<TraineeNote> TraineeNotes => Set<TraineeNote>();
    public DbSet<TraineeNoteHistory> TraineeNoteHistories => Set<TraineeNoteHistory>();
    public DbSet<CommandEnvelope> CommandEnvelopes => Set<CommandEnvelope>();
    public DbSet<ActionExecutionLog> ActionExecutionLogs => Set<ActionExecutionLog>();
    public DbSet<ApiIdempotencyRecord> ApiIdempotencyRecords => Set<ApiIdempotencyRecord>();
    public DbSet<UserTutorialStepProgress> UserTutorialStepProgresses => Set<UserTutorialStepProgress>();
    public DbSet<UserTutorialProgress> UserTutorialProgresses => Set<UserTutorialProgress>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<InAppNotification> InAppNotifications => Set<InAppNotification>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<PushInstallation> PushInstallations => Set<PushInstallation>();
    public DbSet<PushNotificationMessage> PushNotificationMessages => Set<PushNotificationMessage>();
    public DbSet<UserExternalLogin> UserExternalLogins => Set<UserExternalLogin>();
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<PhotoUploadSession> PhotoUploadSessions => Set<PhotoUploadSession>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        TypedIdConventionApplier.ApplyConventions(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        TypedIdConventionApplier.ApplyModelBuilderConverters(modelBuilder);

        SoftDeleteFilterApplier.Apply(modelBuilder);

        AppDbContextEntityTypeConfigurationRegistrar.Apply(modelBuilder);

        RoleSeedDataConfiguration.Apply(modelBuilder);
    }

    private static bool IsEntityBase(Type type)
    {
        var baseType = type.BaseType;
        while (baseType != null)
        {
            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition().Name.StartsWith("EntityBase"))
            {
                return true;
            }
            baseType = baseType.BaseType;
        }
        return false;
    }

    public override int SaveChanges()
    {
        SetTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void SetTimestamps()
    {
        var utcNow = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity == null || !IsEntityBase(entry.Entity.GetType()))
            {
                continue;
            }

            if (entry.State == EntityState.Added)
            {
                var createdAtProperty = entry.Property("CreatedAt");
                if (createdAtProperty.CurrentValue is DateTimeOffset createdAt && createdAt == default)
                {
                    createdAtProperty.CurrentValue = utcNow;
                }

                entry.Property("UpdatedAt").CurrentValue = utcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property("UpdatedAt").CurrentValue = utcNow;
            }
        }
    }
}
