using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ReportingRepositoryTests
{
    [Test]
    public async Task GetPendingOrExpiredRequestsByTraineeId_FiltersAndSortsResults()
    {
        await using var db = CreateDbContext("reporting-repo-requests");
        var traineeId = Id<User>.New();
        var templateId = Id<ReportTemplate>.New();
        var template = new ReportTemplate
        {
            Id = templateId,
            TrainerId = Id<User>.New(),
            Name = "Weekly",
            Fields =
            [
                new ReportTemplateField { Id = Id<ReportTemplateField>.New(), TemplateId = templateId, Key = "b", Order = 2 },
                new ReportTemplateField { Id = Id<ReportTemplateField>.New(), TemplateId = templateId, Key = "a", Order = 1 }
            ]
        };
        db.ReportTemplates.Add(template);
        db.ReportRequests.AddRange(
            new ReportRequest { Id = Id<ReportRequest>.New(), TraineeId = traineeId, TrainerId = Id<User>.New(), TemplateId = templateId, Template = template, Status = ReportRequestStatus.Pending, CreatedAt = DateTimeOffset.UtcNow.AddDays(-1) },
            new ReportRequest { Id = Id<ReportRequest>.New(), TraineeId = traineeId, TrainerId = Id<User>.New(), TemplateId = templateId, Template = template, Status = ReportRequestStatus.Expired, CreatedAt = DateTimeOffset.UtcNow },
            new ReportRequest { Id = Id<ReportRequest>.New(), TraineeId = traineeId, TrainerId = Id<User>.New(), TemplateId = templateId, Template = template, Status = ReportRequestStatus.Submitted, CreatedAt = DateTimeOffset.UtcNow.AddDays(1) });
        await db.SaveChangesAsync();

        var repository = new ReportingRepository(db);
        var results = await repository.GetPendingOrExpiredRequestsByTraineeIdAsync(traineeId);

        results.Should().HaveCount(2);
        results.Select(x => x.Status).Should().Equal(ReportRequestStatus.Expired, ReportRequestStatus.Pending);
        results[0].Template.Fields.Select(x => x.Key).Should().BeEquivalentTo(["a", "b"]);
    }

    [Test]
    public async Task SavePhotoAsync_SoftDeletesExistingActivePhotoAndAddsNewOne()
    {
        await using var db = CreateDbContext("reporting-repo-photo-save");
        var requestId = Id<ReportRequest>.New();
        var existing = new Photo
        {
            Id = Id<Photo>.New(),
            ReportRequestId = requestId,
            OwnerUserId = Id<User>.New(),
            UploaderUserId = Id<User>.New(),
            ViewType = PhotoViewType.Front,
            StorageKey = "photos/old.jpg",
            MimeType = "image/jpeg",
            SizeBytes = 100,
            Checksum = "old",
            IsDeleted = false
        };
        db.Photos.Add(existing);
        await db.SaveChangesAsync();

        var repository = new ReportingRepository(db);
        var replacement = new Photo
        {
            Id = Id<Photo>.New(),
            ReportRequestId = requestId,
            OwnerUserId = existing.OwnerUserId,
            UploaderUserId = existing.UploaderUserId,
            ViewType = PhotoViewType.Front,
            StorageKey = "photos/new.jpg",
            MimeType = "image/jpeg",
            SizeBytes = 120,
            Checksum = "new",
            IsDeleted = false
        };

        await repository.SavePhotoAsync(replacement);
        await db.SaveChangesAsync();

        existing.IsDeleted.Should().BeTrue();
        db.Photos.Should().ContainSingle(x => x.Id == replacement.Id);
    }

    [Test]
    public async Task PhotoAggregates_CountAndBytesRespectFilters()
    {
        await using var db = CreateDbContext("reporting-repo-photo-aggregates");
        var now = DateTimeOffset.UtcNow;
        db.Photos.AddRange(
            CreatePhoto(100, false, now.AddMinutes(-10)),
            CreatePhoto(50, true, now.AddMinutes(-5)),
            CreatePhoto(25, false, now.AddDays(-2)));
        await db.SaveChangesAsync();

        var repository = new ReportingRepository(db);
        var totalBytes = await repository.GetActivePhotoStorageBytesAsync();
        var recentCount = await repository.CountPhotosCreatedSinceAsync(now.AddHours(-1));

        totalBytes.Should().Be(125);
        recentCount.Should().Be(2);
    }

    [Test]
    public async Task SubmissionQueries_FilterByTrainerAndTrainee()
    {
        await using var db = CreateDbContext("reporting-repo-submissions");
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var template = new ReportTemplate { Id = Id<ReportTemplate>.New(), TrainerId = trainerId, Name = "Weekly" };
        var request = new ReportRequest { Id = Id<ReportRequest>.New(), TrainerId = trainerId, TraineeId = traineeId, TemplateId = template.Id, Template = template, Status = ReportRequestStatus.Submitted };
        var submission = new ReportSubmission { Id = Id<ReportSubmission>.New(), ReportRequestId = request.Id, ReportRequest = request, TraineeId = traineeId, PayloadJson = "{}" };
        db.ReportTemplates.Add(template);
        db.ReportRequests.Add(request);
        db.ReportSubmissions.Add(submission);
        await db.SaveChangesAsync();

        var repository = new ReportingRepository(db);
        var byTrainer = await repository.FindSubmissionByIdForTrainerAsync(submission.Id, trainerId, traineeId);
        var byTrainee = await repository.GetSubmissionsByTraineeAsync(traineeId);

        byTrainer.Should().NotBeNull();
        byTrainee.Should().ContainSingle();
    }

    private static AppDbContext CreateDbContext(string name)
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"{name}-{Id<ReportingRepositoryTests>.New():N}")
            .Options);

    private static Photo CreatePhoto(long sizeBytes, bool isDeleted, DateTimeOffset createdAt)
        => new()
        {
            Id = Id<Photo>.New(),
            ReportRequestId = Id<ReportRequest>.New(),
            OwnerUserId = Id<User>.New(),
            UploaderUserId = Id<User>.New(),
            ViewType = PhotoViewType.Front,
            StorageKey = $"photos/{sizeBytes}.jpg",
            MimeType = "image/jpeg",
            SizeBytes = sizeBytes,
            Checksum = "etag",
            IsDeleted = isDeleted,
            CreatedAt = createdAt
        };
}
