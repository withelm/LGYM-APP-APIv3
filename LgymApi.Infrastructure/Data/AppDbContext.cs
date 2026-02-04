using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using Microsoft.EntityFrameworkCore;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => u.Name).IsUnique();
            entity.HasOne(u => u.Plan)
                .WithMany()
                .HasForeignKey(u => u.PlanId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Plan>(entity =>
        {
            entity.ToTable("Plans");
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
            entity.HasIndex(e => new { e.ExerciseId, e.Culture }).IsUnique();
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
