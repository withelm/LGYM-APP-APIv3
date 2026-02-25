using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
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
    public DbSet<EmailNotificationLog> EmailNotificationLogs => Set<EmailNotificationLog>();
    public DbSet<ReportTemplate> ReportTemplates => Set<ReportTemplate>();
    public DbSet<ReportTemplateField> ReportTemplateFields => Set<ReportTemplateField>();
    public DbSet<ReportRequest> ReportRequests => Set<ReportRequest>();
    public DbSet<ReportSubmission> ReportSubmissions => Set<ReportSubmission>();
    public DbSet<SupplementPlan> SupplementPlans => Set<SupplementPlan>();
    public DbSet<SupplementPlanItem> SupplementPlanItems => Set<SupplementPlanItem>();
    public DbSet<SupplementIntakeLog> SupplementIntakeLogs => Set<SupplementIntakeLog>();

    public static readonly Guid UserRoleSeedId = Guid.Parse("f124fe5f-9bf2-45df-bfd2-d5d6be920016");
    public static readonly Guid AdminRoleSeedId = Guid.Parse("1754c6f8-c021-41aa-b610-17088f9476f9");
    public static readonly Guid TesterRoleSeedId = Guid.Parse("f93f03af-ae11-4fd8-a60e-f970f89df6fb");
    public static readonly Guid TrainerRoleSeedId = Guid.Parse("8c1a3db8-72a3-47cc-b3de-f5347c6ae501");
    public static readonly Guid AdminAccessClaimSeedId = Guid.Parse("9dbfd057-cf88-4597-b668-2fdf16a2def6");
    public static readonly Guid ManageUserRolesClaimSeedId = Guid.Parse("97f7ea56-0032-4f18-8703-ab2d1485ad45");
    public static readonly Guid ManageAppConfigClaimSeedId = Guid.Parse("d12f9f84-48f4-4f4b-9614-843f31ea0f96");
    public static readonly Guid ManageGlobalExercisesClaimSeedId = Guid.Parse("27965bf4-ff55-4261-8f98-218ccf00e537");
    private static readonly DateTimeOffset RoleSeedTimestamp = new(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ApplySoftDeleteQueryFilters(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
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
            entity.Property(e => e.Unit).HasConversion<string>();
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
            entity.Property(e => e.Unit).HasConversion<string>();
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
                    Id = UserRoleSeedId,
                    Name = AuthConstants.Roles.User,
                    Description = "Default role for all users",
                    CreatedAt = RoleSeedTimestamp,
                    UpdatedAt = RoleSeedTimestamp
                },
                new Role
                {
                    Id = AdminRoleSeedId,
                    Name = AuthConstants.Roles.Admin,
                    Description = "Administrative privileges",
                    CreatedAt = RoleSeedTimestamp,
                    UpdatedAt = RoleSeedTimestamp
                },
                new Role
                {
                    Id = TesterRoleSeedId,
                    Name = AuthConstants.Roles.Tester,
                    Description = "Excluded from ranking",
                    CreatedAt = RoleSeedTimestamp,
                    UpdatedAt = RoleSeedTimestamp
                },
                new Role
                {
                    Id = TrainerRoleSeedId,
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
                    Id = AdminAccessClaimSeedId,
                    RoleId = AdminRoleSeedId,
                    ClaimType = AuthConstants.PermissionClaimType,
                    ClaimValue = AuthConstants.Permissions.AdminAccess,
                    CreatedAt = RoleSeedTimestamp,
                    UpdatedAt = RoleSeedTimestamp
                },
                new RoleClaim
                {
                    Id = ManageUserRolesClaimSeedId,
                    RoleId = AdminRoleSeedId,
                    ClaimType = AuthConstants.PermissionClaimType,
                    ClaimValue = AuthConstants.Permissions.ManageUserRoles,
                    CreatedAt = RoleSeedTimestamp,
                    UpdatedAt = RoleSeedTimestamp
                },
                new RoleClaim
                {
                    Id = ManageAppConfigClaimSeedId,
                    RoleId = AdminRoleSeedId,
                    ClaimType = AuthConstants.PermissionClaimType,
                    ClaimValue = AuthConstants.Permissions.ManageAppConfig,
                    CreatedAt = RoleSeedTimestamp,
                    UpdatedAt = RoleSeedTimestamp
                },
                new RoleClaim
                {
                    Id = ManageGlobalExercisesClaimSeedId,
                    RoleId = AdminRoleSeedId,
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
                .OnDelete(DeleteBehavior.Cascade);
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

        modelBuilder.Entity<EmailNotificationLog>(entity =>
        {
            entity.ToTable("EmailNotificationLogs");
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.RecipientEmail).IsRequired();
            entity.Property(e => e.PayloadJson).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>();
            entity.HasIndex(e => new { e.Status, e.CreatedAt });
            entity.HasIndex(e => new { e.Type, e.CorrelationId, e.RecipientEmail })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");
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
            if (!typeof(EntityBase).IsAssignableFrom(clrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(clrType, "entity");
            var isDeletedProperty = Expression.Call(
                typeof(EF),
                nameof(EF.Property),
                new[] { typeof(bool) },
                parameter,
                Expression.Constant(nameof(EntityBase.IsDeleted)));
            var compareExpression = Expression.Equal(isDeletedProperty, Expression.Constant(false));
            var lambda = Expression.Lambda(compareExpression, parameter);

            modelBuilder.Entity(clrType).HasQueryFilter(lambda);
        }
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
        foreach (var entry in ChangeTracker.Entries<EntityBase>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAt == default)
                {
                    entry.Entity.CreatedAt = utcNow;
                }

                entry.Entity.UpdatedAt = utcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = utcNow;
            }
        }
    }
}
