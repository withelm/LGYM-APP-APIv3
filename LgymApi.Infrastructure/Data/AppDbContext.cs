using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
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
    public DbSet<ReportRequest> ReportRequests => Set<ReportRequest>();
    public DbSet<ReportSubmission> ReportSubmissions => Set<ReportSubmission>();
    public DbSet<SupplementPlan> SupplementPlans => Set<SupplementPlan>();
    public DbSet<SupplementPlanItem> SupplementPlanItems => Set<SupplementPlanItem>();
    public DbSet<SupplementIntakeLog> SupplementIntakeLogs => Set<SupplementIntakeLog>();
    public DbSet<CommandEnvelope> CommandEnvelopes => Set<CommandEnvelope>();
    public DbSet<ActionExecutionLog> ActionExecutionLogs => Set<ActionExecutionLog>();
    public DbSet<ApiIdempotencyRecord> ApiIdempotencyRecords => Set<ApiIdempotencyRecord>();
    public DbSet<UserTutorialStepProgress> UserTutorialStepProgresses => Set<UserTutorialStepProgress>();
    public DbSet<UserTutorialProgress> UserTutorialProgresses => Set<UserTutorialProgress>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<InAppNotification> InAppNotifications => Set<InAppNotification>();

    public static readonly Id<Role> UserRoleSeedId = ParseSeedId<Role>("f124fe5f-9bf2-45df-bfd2-d5d6be920016");
    public static readonly Id<Role> AdminRoleSeedId = ParseSeedId<Role>("1754c6f8-c021-41aa-b610-17088f9476f9");
    public static readonly Id<Role> TesterRoleSeedId = ParseSeedId<Role>("f93f03af-ae11-4fd8-a60e-f970f89df6fb");
    public static readonly Id<Role> TrainerRoleSeedId = ParseSeedId<Role>("8c1a3db8-72a3-47cc-b3de-f5347c6ae501");
    public static readonly Id<RoleClaim> AdminAccessClaimSeedId = ParseSeedId<RoleClaim>("9dbfd057-cf88-4597-b668-2fdf16a2def6");
    public static readonly Id<RoleClaim> ManageUserRolesClaimSeedId = ParseSeedId<RoleClaim>("97f7ea56-0032-4f18-8703-ab2d1485ad45");
    public static readonly Id<RoleClaim> ManageAppConfigClaimSeedId = ParseSeedId<RoleClaim>("d12f9f84-48f4-4f4b-9614-843f31ea0f96");
    public static readonly Id<RoleClaim> ManageGlobalExercisesClaimSeedId = ParseSeedId<RoleClaim>("27965bf4-ff55-4261-8f98-218ccf00e537");
    private static readonly DateTimeOffset RoleSeedTimestamp = new(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // T5: Register typed-ID value converters for all entity types.
        // These converters enable Id<TEntity> and Id<TEntity>? to map to/from GUID columns
        // in the database without changing schema (GUID columns remain GUID-backed).
        
        // Discover all entity types that inherit from EntityBase<T>
        // Load types from Domain assembly where entities are defined
        var domainAssembly = typeof(EntityBase<>).Assembly;
        var entityTypes = domainAssembly
            .GetTypes()
            .Where(t => t.Namespace == "LgymApi.Domain.Entities" && !t.IsAbstract && t.IsClass)
            .Where(t =>
            {
                var baseType = t.BaseType;
                while (baseType != null)
                {
                    // Check if this is EntityBase<T> by looking at generic type definition
                    if (baseType.IsGenericType && 
                        baseType.GetGenericTypeDefinition().Name == "EntityBase`1")
                    {
                        return true;
                    }
                    baseType = baseType.BaseType;
                }
                return false;
            })
            .ToList();

        // Register converters for each entity type
        foreach (var entityType in entityTypes)
        {
            var idType = typeof(Id<>).MakeGenericType(entityType);
            var nullableIdType = typeof(Nullable<>).MakeGenericType(idType);
            
            var converterType = typeof(TypedIdValueConverter<>).MakeGenericType(entityType);
            var nullableConverterType = typeof(NullableTypedIdValueConverter<>).MakeGenericType(entityType);

            // Register conversion for Id<TEntity> properties
            configurationBuilder.Properties(idType).HaveConversion(converterType);
            
            // Register conversion for nullable Id<TEntity>? properties
            configurationBuilder.Properties(nullableIdType).HaveConversion(nullableConverterType);
        }

        configurationBuilder.Properties<Id<CorrelationScope>>().HaveConversion<TypedIdValueConverter<CorrelationScope>>();
        configurationBuilder.Properties<Id<CorrelationScope>?>().HaveConversion<NullableTypedIdValueConverter<CorrelationScope>>();

    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // T5: Explicitly configure typed-ID converters with comparers to enable key validation
        // Register each entity type that has a typed ID property
        var entityTypesWithTypedIds = typeof(AppDbContext).Assembly
            .GetTypes()
            .Where(t => t.Namespace == "LgymApi.Domain.Entities" && !t.IsAbstract && t.IsClass)
            .Where(t =>
            {
                var baseType = t.BaseType;
                while (baseType != null)
                {
                    if (baseType.IsGenericType && 
                        baseType.GetGenericTypeDefinition().Name == "EntityBase`1")
                    {
                        return true;
                    }
                    baseType = baseType.BaseType;
                }
                return false;
            })
            .ToList();

        foreach (var entityType in entityTypesWithTypedIds)
        {
            var converterType = typeof(TypedIdValueConverter<>).MakeGenericType(entityType);
            var nullableConverterType = typeof(NullableTypedIdValueConverter<>).MakeGenericType(entityType);
            
            var converterInstance = (dynamic)Activator.CreateInstance(converterType)!;
            var nullableConverterInstance = (dynamic)Activator.CreateInstance(nullableConverterType)!;

            // Use reflection to call Entity<T>().Property(...).HasConversion(converter)
            var entityMethod = typeof(ModelBuilder)
                .GetMethod("Entity", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                ?.MakeGenericMethod(entityType);
            
            if (entityMethod == null)
                continue;

            dynamic entityBuilder = entityMethod.Invoke(modelBuilder, null)!;
            
            // Configure Id property
            var propertyMethod = entityBuilder.GetType()
                .GetMethod("Property", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                ?.MakeGenericMethod(typeof(Id<>).MakeGenericType(entityType));
            
            if (propertyMethod == null)
                continue;

            var idParam = System.Linq.Expressions.Expression.Parameter(entityType, "e");
            var idProp = System.Linq.Expressions.Expression.Property(idParam, "Id");
            var idLambda = System.Linq.Expressions.Expression.Lambda(idProp, idParam);
            
            dynamic propBuilder = propertyMethod.Invoke(entityBuilder, new object[] { idLambda })!;
            var hasConversionMethod = propBuilder.GetType()
                .GetMethod("HasConversion", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                ?.MakeGenericMethod(converterInstance.GetType().GetProperty("ModelClrType")!.GetValue(converterInstance));
            
            hasConversionMethod?.Invoke(propBuilder, new object?[] { converterInstance });
        }

        ApplySoftDeleteQueryFilters(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(u => u.Email)
                .HasConversion(email => email.Value, value => new Email(value));
            entity.HasIndex(u => u.Email)
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");
            entity.HasIndex(u => u.Name)
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");
            entity.HasOne(u => u.Plan)
                .WithMany()
                .HasForeignKey(u => u.PlanId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Plan>(entity =>
        {
            entity.ToTable("Plans");
            entity.HasIndex(p => p.ShareCode)
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE AND \"ShareCode\" IS NOT NULL");
            entity.HasOne(p => p.User)
                .WithMany(u => u.Plans)
                .HasForeignKey(p => p.UserId);
        });

        modelBuilder.Entity<PlanDay>(entity =>
        {
            entity.ToTable("PlanDays");
            entity.HasOne(p => p.Plan)
                .WithMany(p => p.PlanDays)
                .HasForeignKey(p => p.PlanId);
        });

        modelBuilder.Entity<PlanDayExercise>(entity =>
        {
            entity.ToTable("PlanDayExercises");
            entity.HasOne(e => e.PlanDay)
                .WithMany(p => p.Exercises)
                .HasForeignKey(e => e.PlanDayId);
            entity.HasOne(e => e.Exercise)
                .WithMany()
                .HasForeignKey(e => e.ExerciseId);
        });

        modelBuilder.Entity<Exercise>(entity =>
        {
            entity.ToTable("Exercises");
            entity.Property(e => e.BodyPart).HasConversion<string>();
        });

        modelBuilder.Entity<ExerciseTranslation>(entity =>
        {
            entity.ToTable("ExerciseTranslations");
            entity.Property(e => e.Culture).HasMaxLength(16).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(e => new { e.ExerciseId, e.Culture })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");
            entity.HasOne(e => e.Exercise)
                .WithMany(e => e.Translations)
                .HasForeignKey(e => e.ExerciseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Training>(entity =>
        {
            entity.ToTable("Trainings");
            entity.HasOne(t => t.User)
                .WithMany(u => u.Trainings)
                .HasForeignKey(t => t.UserId);
            entity.HasOne(t => t.PlanDay)
                .WithMany()
                .HasForeignKey(t => t.TypePlanDayId);
            entity.HasOne(t => t.Gym)
                .WithMany(g => g.Trainings)
                .HasForeignKey(t => t.GymId);
        });

        modelBuilder.Entity<TrainingExerciseScore>(entity =>
        {
            entity.ToTable("TrainingExerciseScores");
            entity.HasOne(t => t.Training)
                .WithMany(t => t.Exercises)
                .HasForeignKey(t => t.TrainingId);
            entity.HasOne(t => t.ExerciseScore)
                .WithMany()
                .HasForeignKey(t => t.ExerciseScoreId);
        });

        modelBuilder.Entity<ExerciseScore>(entity =>
        {
            entity.ToTable("ExerciseScores");
            entity.Ignore(e => e.Weight);
            entity.Property(e => e.WeightValue).HasField("_weightValue").HasColumnName("Weight");
            entity.Property(e => e.Unit).HasField("_unit").HasConversion<string>();
            entity.HasOne(e => e.Exercise)
                .WithMany(e => e.ExerciseScores)
                .HasForeignKey(e => e.ExerciseId);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId);
            entity.HasOne(e => e.Training)
                .WithMany()
                .HasForeignKey(e => e.TrainingId);
        });

        modelBuilder.Entity<Measurement>(entity =>
        {
            entity.ToTable("Measurements");
            entity.Property(e => e.BodyPart).HasConversion<string>();
        });

        modelBuilder.Entity<MainRecord>(entity =>
        {
            entity.ToTable("MainRecords");
            entity.Ignore(e => e.Weight);
            entity.Property(e => e.WeightValue).HasField("_weightValue").HasColumnName("Weight");
            entity.Property(e => e.Unit).HasField("_unit").HasConversion<string>();
        });

        modelBuilder.Entity<Gym>(entity =>
        {
            entity.ToTable("Gyms");
            entity.HasOne(g => g.Address)
                .WithMany()
                .HasForeignKey(g => g.AddressId);
        });

        modelBuilder.Entity<Address>(entity =>
        {
            entity.ToTable("Addresses");
        });

        modelBuilder.Entity<EloRegistry>(entity =>
        {
            entity.ToTable("EloRegistries");
            entity.Property(e => e.Elo)
                .HasConversion(elo => elo.Value, value => new Domain.ValueObjects.Elo(value));
            entity.HasOne(e => e.User)
                .WithMany(u => u.EloRegistries)
                .HasForeignKey(e => e.UserId);
        });

        modelBuilder.Entity<AppConfig>(entity =>
        {
            entity.ToTable("AppConfigs");
            entity.Property(e => e.Platform).HasConversion<string>();
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.Property(e => e.Name).IsRequired();
            entity.HasIndex(e => e.Name)
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");
            entity.HasData(
                new Role
                {
                    Id = (Id<Role>)UserRoleSeedId,
                    Name = AuthConstants.Roles.User,
                    Description = "Default role for all users",
                    CreatedAt = RoleSeedTimestamp,
                    UpdatedAt = RoleSeedTimestamp
                },
                new Role
                {
                    Id = (Id<Role>)AdminRoleSeedId,
                    Name = AuthConstants.Roles.Admin,
                    Description = "Administrative privileges",
                    CreatedAt = RoleSeedTimestamp,
                    UpdatedAt = RoleSeedTimestamp
                },
                new Role
                {
                    Id = (Id<Role>)TesterRoleSeedId,
                    Name = AuthConstants.Roles.Tester,
                    Description = "Excluded from ranking",
                    CreatedAt = RoleSeedTimestamp,
                    UpdatedAt = RoleSeedTimestamp
                },
                new Role
                {
                    Id = (Id<Role>)TrainerRoleSeedId,
                    Name = AuthConstants.Roles.Trainer,
                    Description = "Trainer role for coach-facing APIs",
                    CreatedAt = RoleSeedTimestamp,
                    UpdatedAt = RoleSeedTimestamp
                });
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("UserRoles");
            entity.HasKey(e => new { e.UserId, e.RoleId });
            entity.HasOne(e => e.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RoleClaim>(entity =>
        {
            entity.ToTable("RoleClaims");
            entity.Property(e => e.ClaimType).IsRequired();
            entity.Property(e => e.ClaimValue).IsRequired();
            entity.HasIndex(e => new { e.RoleId, e.ClaimType, e.ClaimValue })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");
            entity.HasOne(e => e.Role)
                .WithMany(r => r.RoleClaims)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasData(
                new RoleClaim
                {
                    Id = (Id<RoleClaim>)AdminAccessClaimSeedId,
                    RoleId = (Id<Role>)AdminRoleSeedId,
                    ClaimType = AuthConstants.PermissionClaimType,
                    ClaimValue = AuthConstants.Permissions.AdminAccess,
                    CreatedAt = RoleSeedTimestamp,
                    UpdatedAt = RoleSeedTimestamp
                },
                new RoleClaim
                {
                    Id = (Id<RoleClaim>)ManageUserRolesClaimSeedId,
                    RoleId = (Id<Role>)AdminRoleSeedId,
                    ClaimType = AuthConstants.PermissionClaimType,
                    ClaimValue = AuthConstants.Permissions.ManageUserRoles,
                    CreatedAt = RoleSeedTimestamp,
                    UpdatedAt = RoleSeedTimestamp
                },
                new RoleClaim
                {
                    Id = (Id<RoleClaim>)ManageAppConfigClaimSeedId,
                    RoleId = (Id<Role>)AdminRoleSeedId,
                    ClaimType = AuthConstants.PermissionClaimType,
                    ClaimValue = AuthConstants.Permissions.ManageAppConfig,
                    CreatedAt = RoleSeedTimestamp,
                    UpdatedAt = RoleSeedTimestamp
                },
                new RoleClaim
                {
                    Id = (Id<RoleClaim>)ManageGlobalExercisesClaimSeedId,
                    RoleId = (Id<Role>)AdminRoleSeedId,
                    ClaimType = AuthConstants.PermissionClaimType,
                    ClaimValue = AuthConstants.Permissions.ManageGlobalExercises,
                    CreatedAt = RoleSeedTimestamp,
                    UpdatedAt = RoleSeedTimestamp
                });
        });

        modelBuilder.Entity<TrainerInvitation>(entity =>
        {
            entity.ToTable("TrainerInvitations");
            entity.Property(e => e.Code).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>();
            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");
            entity.HasOne(e => e.Trainer)
                .WithMany()
                .HasForeignKey(e => e.TrainerId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Trainee)
                .WithMany()
                .HasForeignKey(e => e.TraineeId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TrainerTraineeLink>(entity =>
        {
            entity.ToTable("TrainerTraineeLinks");
            entity.HasIndex(e => e.TraineeId)
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");
            entity.HasIndex(e => new { e.TrainerId, e.TraineeId });
            entity.HasOne(e => e.Trainer)
                .WithMany()
                .HasForeignKey(e => e.TrainerId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Trainee)
                .WithMany()
                .HasForeignKey(e => e.TraineeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NotificationMessage>(entity =>
        {
            entity.ToTable("NotificationMessages");
            entity.Property(e => e.Type)
                .HasConversion(
                    notificationType => notificationType.Value,
                    value => EmailNotificationType.Parse(value))
                .IsRequired();
            entity.Property(e => e.Recipient)
                .HasConversion(email => email.Value, value => new Email(value));
            entity.Property(e => e.Recipient).IsRequired();
            entity.Property(e => e.PayloadJson).IsRequired();
            entity.HasIndex(e => new { e.Status, e.NextAttemptAt, e.CreatedAt });
            entity.HasIndex(e => new { e.Channel, e.Type, e.CorrelationId, e.Recipient })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");
        });

        modelBuilder.Entity<EmailNotificationSubscription>(entity =>
        {
            entity.ToTable("EmailNotificationSubscriptions");
            entity.Property(e => e.NotificationType).IsRequired();
            entity.HasIndex(e => new { e.UserId, e.NotificationType })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReportTemplate>(entity =>
        {
            entity.ToTable("ReportTemplates");
            entity.Property(e => e.Name).HasMaxLength(120).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.HasIndex(e => new { e.TrainerId, e.Name });
            entity.HasOne(e => e.Trainer)
                .WithMany()
                .HasForeignKey(e => e.TrainerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReportTemplateField>(entity =>
        {
            entity.ToTable("ReportTemplateFields");
            entity.Property(e => e.Key).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Label).HasMaxLength(120).IsRequired();
            entity.Property(e => e.Type).HasConversion<string>();
            entity.HasIndex(e => new { e.TemplateId, e.Key })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");
            entity.HasOne(e => e.Template)
                .WithMany(e => e.Fields)
                .HasForeignKey(e => e.TemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReportRequest>(entity =>
        {
            entity.ToTable("ReportRequests");
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.HasIndex(e => new { e.TrainerId, e.TraineeId, e.CreatedAt });
            entity.HasIndex(e => new { e.TraineeId, e.Status, e.CreatedAt });
            entity.HasOne(e => e.Trainer)
                .WithMany()
                .HasForeignKey(e => e.TrainerId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Trainee)
                .WithMany()
                .HasForeignKey(e => e.TraineeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Template)
                .WithMany()
                .HasForeignKey(e => e.TemplateId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Submission)
                .WithOne(e => e.ReportRequest)
                .HasForeignKey<ReportSubmission>(e => e.ReportRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReportSubmission>(entity =>
        {
            entity.ToTable("ReportSubmissions");
            entity.Property(e => e.PayloadJson).IsRequired();
            entity.HasIndex(e => e.ReportRequestId)
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");
            entity.HasOne(e => e.Trainee)
                .WithMany()
                .HasForeignKey(e => e.TraineeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SupplementPlan>(entity =>
        {
            entity.ToTable("SupplementPlans");
            entity.Property(e => e.Name).HasMaxLength(120).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.HasIndex(e => new { e.TrainerId, e.TraineeId, e.CreatedAt });
            entity.HasIndex(e => new { e.TraineeId, e.IsActive });
            entity.HasOne(e => e.Trainer)
                .WithMany()
                .HasForeignKey(e => e.TrainerId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Trainee)
                .WithMany()
                .HasForeignKey(e => e.TraineeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SupplementPlanItem>(entity =>
        {
            entity.ToTable("SupplementPlanItems");
            entity.Property(e => e.SupplementName).HasMaxLength(160).IsRequired();
            entity.Property(e => e.Dosage).HasMaxLength(120).IsRequired();
            entity.Property(e => e.DaysOfWeekMask)
                .HasConversion(mask => (int)mask, value => (DaysOfWeekSet)value);
            entity.HasIndex(e => new { e.PlanId, e.Order, e.TimeOfDay });
            entity.HasOne(e => e.Plan)
                .WithMany(e => e.Items)
                .HasForeignKey(e => e.PlanId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SupplementIntakeLog>(entity =>
        {
            entity.ToTable("SupplementIntakeLogs");
            entity.HasIndex(e => new { e.TraineeId, e.PlanItemId, e.IntakeDate })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");
            entity.HasOne(e => e.Trainee)
                .WithMany()
                .HasForeignKey(e => e.TraineeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.PlanItem)
                .WithMany()
                .HasForeignKey(e => e.PlanItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CommandEnvelope>(entity =>
        {
            entity.ToTable("CommandEnvelopes");
            var idProp = entity.Property(e => e.Id);
            idProp.HasConversion(typeof(TypedIdValueConverter<CommandEnvelope>));
            var metadata = entity.Metadata.FindProperty("Id");
            if (metadata != null)
            {
                var comparerType = typeof(IdValueComparer<>).MakeGenericType(typeof(CommandEnvelope));
                var comparer = Activator.CreateInstance(comparerType);
                if (comparer != null)
                {
                    var setMethod = metadata.GetType()
                        .GetMethod("SetValueComparer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    setMethod?.Invoke(metadata, new[] { comparer });
                }
            }
            entity.Property(e => e.PayloadJson).IsRequired();
            entity.Property(e => e.CommandTypeFullName).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>();
            // CRITICAL: Unique constraint on CorrelationId to fix duplicate race condition (Issue #232)
            // This enforces DB-level duplicate protection for concurrent dispatch attempts
            entity.HasIndex(e => e.CorrelationId)
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");
            // Work retrieval index: (Status, NextAttemptAt) for pending retries
            entity.HasIndex(e => new { e.Status, e.NextAttemptAt })
                .HasFilter("\"IsDeleted\" = FALSE");
            // One-to-many relationship with ExecutionLog
            entity.HasMany(e => e.ExecutionLogs)
                .WithOne(l => l.CommandEnvelope)
                .HasForeignKey(l => l.CommandEnvelopeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ActionExecutionLog>(entity =>
        {
            entity.ToTable("ActionExecutionLogs");
            var idProp = entity.Property(e => e.Id);
            idProp.HasConversion(typeof(TypedIdValueConverter<ActionExecutionLog>));
            var idMetadata = entity.Metadata.FindProperty("Id");
            if (idMetadata != null)
            {
                var idComparerType = typeof(IdValueComparer<>).MakeGenericType(typeof(ActionExecutionLog));
                var idComparer = Activator.CreateInstance(idComparerType);
                if (idComparer != null)
                {
                    var setMethod = idMetadata.GetType()
                        .GetMethod("SetValueComparer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    setMethod?.Invoke(idMetadata, new[] { idComparer });
                }
            }
            entity.Property(e => e.ActionType).HasConversion<string>();
            entity.Property(e => e.HandlerTypeName);
            entity.Property(e => e.Status).HasConversion<string>();
            // Index for finding execution history by envelope and status
            entity.HasIndex(e => new { e.CommandEnvelopeId, e.Status })
                .HasFilter("\"IsDeleted\" = FALSE");
            // Index for finding logs by action type (supports filtering by operation kind)
            entity.HasIndex(e => new { e.CommandEnvelopeId, e.ActionType })
                .HasFilter("\"IsDeleted\" = FALSE");
            // Timestamp index for retrieving recent logs efficiently
            entity.HasIndex(e => e.CreatedAt)
                .HasFilter("\"IsDeleted\" = FALSE");
            // Foreign key is configured by CommandEnvelope HasMany
        });

        modelBuilder.Entity<UserTutorialProgress>(entity =>
        {
            entity.ToTable("UserTutorialProgresses");
            entity.Property(utp => utp.TutorialType).HasConversion<string>();
            entity.HasOne(utp => utp.User)
                .WithMany()
                .HasForeignKey(utp => utp.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            // Unique index on UserId + TutorialType (enforces one progress per user per tutorial)
            entity.HasIndex(utp => new { utp.UserId, utp.TutorialType })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");
            // Index on UserId + IsCompleted (for querying completion status)
            entity.HasIndex(utp => new { utp.UserId, utp.IsCompleted })
                .HasFilter("\"IsDeleted\" = FALSE");
        });

        modelBuilder.Entity<UserTutorialStepProgress>(entity =>
        {
            entity.ToTable("UserTutorialStepProgresses");
            entity.Property(utsp => utsp.TutorialStep).HasConversion<string>();
            entity.HasOne(utsp => utsp.UserTutorialProgress)
                .WithMany(utp => utp.CompletedSteps)
                .HasForeignKey(utsp => utsp.UserTutorialProgressId)
                .OnDelete(DeleteBehavior.Cascade);
            // Unique index on UserTutorialProgressId + TutorialStep (prevents duplicate step completions)
            entity.HasIndex(utsp => new { utsp.UserTutorialProgressId, utsp.TutorialStep })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");
         });

        modelBuilder.Entity<ApiIdempotencyRecord>(entity =>
        {
            entity.ToTable("ApiIdempotencyRecords");
            entity.Property(e => e.IdempotencyKey).IsRequired().HasMaxLength(256);
            entity.Property(e => e.ScopeTuple).IsRequired().HasMaxLength(512);
            entity.Property(e => e.RequestFingerprint).IsRequired().HasMaxLength(64);
            entity.Property(e => e.ResponseBodyJson).IsRequired();
            // CRITICAL: Unique constraint on (ScopeTuple, IdempotencyKey) for API-level duplicate protection (Issue #232)
            // Enforces one idempotency record per endpoint scope + key combination
            entity.HasIndex(e => new { e.ScopeTuple, e.IdempotencyKey })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");
            // Lookup index for replay/conflict checks
            entity.HasIndex(e => new { e.ScopeTuple, e.IdempotencyKey, e.RequestFingerprint })
                .HasFilter("\"IsDeleted\" = FALSE");
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("PasswordResetTokens");
            entity.HasIndex(e => e.TokenHash)
                .IsUnique();
            entity.HasIndex(e => new { e.UserId, e.IsUsed, e.ExpiresAt })
                .HasFilter("\"IsDeleted\" = FALSE");
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InAppNotification>(entity =>
        {
            entity.ToTable("in_app_notifications");
            entity.Property(e => e.Type)
                .HasConversion(
                    notificationType => notificationType.Value,
                    value => InAppNotificationType.Parse(value))
                .IsRequired();
            entity.HasIndex(e => new { e.RecipientId, e.CreatedAt, e.Id });
        });
     }

     private static void ApplySoftDeleteQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.IsOwned())
            {
                continue;
            }

            var clrType = entityType.ClrType;
            if (!IsEntityBase(clrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(clrType, "entity");
            var isDeletedProperty = Expression.Call(
                typeof(EF),
                nameof(EF.Property),
                new[] { typeof(bool) },
                parameter,
                Expression.Constant("IsDeleted"));
            var compareExpression = Expression.Equal(isDeletedProperty, Expression.Constant(false));
            var lambda = Expression.Lambda(compareExpression, parameter);

            modelBuilder.Entity(clrType).HasQueryFilter(lambda);
        }
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

    private static Id<TEntity> ParseSeedId<TEntity>(string idString)
    {
        if (!Id<TEntity>.TryParse(idString, out var id))
        {
            throw new InvalidOperationException($"Failed to parse seed ID: {idString}");
        }
        return id;
    }
}
