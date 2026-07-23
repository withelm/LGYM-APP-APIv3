using FluentAssertions;
using LgymApi.Application.Abstractions.Storage;
using LgymApi.Application.Coaching.Contracts.Access;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Options;
using LgymApi.Application.Repositories;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace LgymApi.UnitTests;

internal static class PhotoServiceTestFactory
{
    public static User CreateUser(Id<User> id, string email) => new()
    {
        Id = id,
        Name = "Test User",
        Email = email,
        ProfileRank = "Rookie"
    };

    public static ReportRequest CreateReportRequest(Id<ReportRequest> id, Id<User> traineeId) => new()
    {
        Id = id,
        TraineeId = traineeId,
        TrainerId = Id<User>.New(),
        TemplateId = Id<ReportTemplate>.New(),
        Status = ReportRequestStatus.Pending,
        IsDeleted = false
    };

    public static IReportingService CreateService(
        Func<Id<ReportRequest>, CancellationToken, Task<ReportRequest?>>? findRequestById = null,
        Func<Id<User>, Id<User>, CancellationToken, Task<CoachingRelationshipAccessDecision>>? relationshipAccess = null,
        IPhotoStorageProvider? photoStorageProvider = null,
        IReportingRepository? reportingRepository = null,
        PendingPhotoUpload? pendingUpload = null,
        IUnitOfWork? unitOfWork = null,
        IPhotoUploadInitTracker? photoUploadInitTracker = null,
        PhotoStorageOptions? photoStorageOptions = null)
    {
        var roleRepository = Substitute.For<IRoleRepository>();
        var coachingRelationshipAccess = Substitute.For<ICoachingRelationshipAccessService>();
        coachingRelationshipAccess.GetAccessDecisionAsync(Arg.Any<Id<User>>(), Arg.Any<Id<User>>(), Arg.Any<CancellationToken>())
            .Returns(ci => relationshipAccess?.Invoke(ci.ArgAt<Id<User>>(0), ci.ArgAt<Id<User>>(1), ci.ArgAt<CancellationToken>(2))
                ?? Task.FromResult(new CoachingRelationshipAccessDecision(false, false)));

        var repo = reportingRepository ?? Substitute.For<IReportingRepository>();
        if (findRequestById != null)
        {
            repo.FindRequestByIdAsync(Arg.Any<Id<ReportRequest>>(), Arg.Any<CancellationToken>())
                .Returns(ci => findRequestById(ci.ArgAt<Id<ReportRequest>>(0), ci.ArgAt<CancellationToken>(1)));
        }

        var commandDispatcher = Substitute.For<ICommandDispatcher>();
        var resolvedUnitOfWork = unitOfWork ?? Substitute.For<IUnitOfWork>();

        var storageProvider = photoStorageProvider ?? Substitute.For<IPhotoStorageProvider>();
        var uploadInitTracker = photoUploadInitTracker ?? Substitute.For<IPhotoUploadInitTracker>();
        uploadInitTracker.CountRecentUploadInitsAsync(Arg.Any<Id<User>>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(0);
        uploadInitTracker.GetUploadSessionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var storageKey = ci.ArgAt<string>(0);
                return pendingUpload != null && string.Equals(pendingUpload.StorageKey, storageKey, StringComparison.Ordinal)
                    ? pendingUpload
                    : null;
            });

        var dependencies = Substitute.For<IReportingServiceDependencies>();
        dependencies.RoleRepository.Returns(roleRepository);
        dependencies.CoachingRelationshipAccessService.Returns(coachingRelationshipAccess);
        dependencies.ReportingRepository.Returns(repo);
        dependencies.CommandDispatcher.Returns(commandDispatcher);
        dependencies.UnitOfWork.Returns(resolvedUnitOfWork);
        dependencies.PhotoStorageProvider.Returns(storageProvider);
        dependencies.PhotoUploadInitTracker.Returns(uploadInitTracker);
        dependencies.Logger.Returns(Substitute.For<ILogger<ReportingService>>());
        dependencies.PhotoStorageOptions.Returns(photoStorageOptions ?? new PhotoStorageOptions());

        return new ReportingService(dependencies);
    }
}
