using System.Text.Json;
using FluentAssertions;
using FluentValidation.TestHelper;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Features.Trainer.Validation;
using LgymApi.Application.Abstractions.Storage;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Options;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ReportingServiceTests
{
    private readonly UpsertReportTemplateRequestValidator _validator = new();

    [Test]
    public void ValidMeasurementsModuleTemplate_ShouldNotHaveErrors()
    {
        var request = new UpsertReportTemplateRequest
        {
            Name = "Monthly Progress Report",
            Fields =
            [
                new ReportTemplateFieldRequest
                {
                    Key = "body_measurements",
                    Label = "Body Measurements",
                    Type = ReportFieldType.Measurements,
                    IsRequired = true,
                    Order = 1,
                    ModuleConfig = JsonDocument.Parse("""
                        {
                            "measurementTypes": ["weight", "bodyFat", "chest", "waist"]
                        }
                        """).RootElement
                }
            ]
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void MeasurementsField_WithMissingModuleConfig_ShouldHaveError()
    {
        var request = new UpsertReportTemplateRequest
        {
            Name = "Monthly Progress Report",
            Fields =
            [
                new ReportTemplateFieldRequest
                {
                    Key = "body_measurements",
                    Label = "Body Measurements",
                    Type = ReportFieldType.Measurements,
                    IsRequired = true,
                    Order = 1,
                    ModuleConfig = null
                }
            ]
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Fields[0].ModuleConfig");
    }

    [Test]
    public void MeasurementsField_WithEmptyMeasurementTypes_ShouldHaveError()
    {
        var request = new UpsertReportTemplateRequest
        {
            Name = "Monthly Progress Report",
            Fields =
            [
                new ReportTemplateFieldRequest
                {
                    Key = "body_measurements",
                    Label = "Body Measurements",
                    Type = ReportFieldType.Measurements,
                    IsRequired = true,
                    Order = 1,
                    ModuleConfig = JsonDocument.Parse("""
                        {
                            "measurementTypes": []
                        }
                        """).RootElement
                }
            ]
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Fields[0].ModuleConfig");
    }

    [Test]
    public void MeasurementsField_WithMissingMeasurementTypesProperty_ShouldHaveError()
    {
        var request = new UpsertReportTemplateRequest
        {
            Name = "Monthly Progress Report",
            Fields =
            [
                new ReportTemplateFieldRequest
                {
                    Key = "body_measurements",
                    Label = "Body Measurements",
                    Type = ReportFieldType.Measurements,
                    IsRequired = true,
                    Order = 1,
                    ModuleConfig = JsonDocument.Parse("""
                        {
                            "wrongProperty": ["weight", "chest"]
                        }
                        """).RootElement
                }
            ]
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Fields[0].ModuleConfig");
    }

    [Test]
    public void MeasurementsField_WithAllValidMeasurementTypes_ShouldNotHaveErrors()
    {
        var request = new UpsertReportTemplateRequest
        {
            Name = "Comprehensive Body Measurements",
            Fields =
            [
                new ReportTemplateFieldRequest
                {
                    Key = "full_body_measurements",
                    Label = "Full Body Measurements",
                    Type = ReportFieldType.Measurements,
                    IsRequired = true,
                    Order = 1,
                    ModuleConfig = JsonDocument.Parse("""
                        {
                            "measurementTypes": ["weight", "bodyFat", "chest", "waist", "hips", "thighs", "biceps", "calves"]
                        }
                        """).RootElement
                }
            ]
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void MixedFieldTypesTemplate_WithValidFields_ShouldNotHaveErrors()
    {
        var request = new UpsertReportTemplateRequest
        {
            Name = "Complete Progress Report",
            Fields =
            [
                new ReportTemplateFieldRequest
                {
                    Key = "overall_feedback",
                    Label = "Overall Feedback",
                    Type = ReportFieldType.Text,
                    IsRequired = true,
                    Order = 1,
                    ModuleConfig = null
                },
                new ReportTemplateFieldRequest
                {
                    Key = "progress_photos",
                    Label = "Progress Photos",
                    Type = ReportFieldType.Photos,
                    IsRequired = true,
                    Order = 2,
                    ModuleConfig = JsonDocument.Parse("""
                        {
                            "requiredViews": ["front", "side", "back"]
                        }
                        """).RootElement
                },
                new ReportTemplateFieldRequest
                {
                    Key = "body_measurements",
                    Label = "Body Measurements",
                    Type = ReportFieldType.Measurements,
                    IsRequired = true,
                    Order = 3,
                    ModuleConfig = JsonDocument.Parse("""
                        {
                            "measurementTypes": ["weight", "bodyFat", "chest", "waist"]
                        }
                        """).RootElement
                },
                new ReportTemplateFieldRequest
                {
                    Key = "current_weight",
                    Label = "Current Weight (kg)",
                    Type = ReportFieldType.Number,
                    IsRequired = true,
                    Order = 4,
                    ModuleConfig = null
                },
                new ReportTemplateFieldRequest
                {
                    Key = "training_completed",
                    Label = "Did you complete all training sessions?",
                    Type = ReportFieldType.Boolean,
                    IsRequired = true,
                    Order = 5,
                    ModuleConfig = null
                }
            ]
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void PhotosField_WithValidRequiredViews_ShouldNotHaveErrors()
    {
        var request = new UpsertReportTemplateRequest
        {
            Name = "Photo Progress Report",
            Fields =
            [
                new ReportTemplateFieldRequest
                {
                    Key = "progress_photos",
                    Label = "Progress Photos",
                    Type = ReportFieldType.Photos,
                    IsRequired = true,
                    Order = 1,
                    ModuleConfig = JsonDocument.Parse("""
                        {
                            "requiredViews": ["front", "side", "back"]
                        }
                        """).RootElement
                }
            ]
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void PhotosField_WithMissingModuleConfig_ShouldHaveError()
    {
        var request = new UpsertReportTemplateRequest
        {
            Name = "Photo Progress Report",
            Fields =
            [
                new ReportTemplateFieldRequest
                {
                    Key = "progress_photos",
                    Label = "Progress Photos",
                    Type = ReportFieldType.Photos,
                    IsRequired = true,
                    Order = 1,
                    ModuleConfig = null
                }
            ]
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Fields[0].ModuleConfig");
    }

    [Test]
    public void PhotosField_WithEmptyRequiredViews_ShouldHaveError()
    {
        var request = new UpsertReportTemplateRequest
        {
            Name = "Photo Progress Report",
            Fields =
            [
                new ReportTemplateFieldRequest
                {
                    Key = "progress_photos",
                    Label = "Progress Photos",
                    Type = ReportFieldType.Photos,
                    IsRequired = true,
                    Order = 1,
                    ModuleConfig = JsonDocument.Parse("""
                        {
                            "requiredViews": []
                        }
                        """).RootElement
                }
            ]
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Fields[0].ModuleConfig");
    }

    [Test]
    public void PhotosField_WithMissingRequiredViewsProperty_ShouldHaveError()
    {
        var request = new UpsertReportTemplateRequest
        {
            Name = "Photo Progress Report",
            Fields =
            [
                new ReportTemplateFieldRequest
                {
                    Key = "progress_photos",
                    Label = "Progress Photos",
                    Type = ReportFieldType.Photos,
                    IsRequired = true,
                    Order = 1,
                    ModuleConfig = JsonDocument.Parse("""
                        {
                            "wrongProperty": ["front", "side"]
                        }
                        """).RootElement
                }
            ]
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Fields[0].ModuleConfig");
    }

    [Test]
    public void PhotosField_WithInvalidRequiredView_ShouldHaveError()
    {
        var request = new UpsertReportTemplateRequest
        {
            Name = "Photo Progress Report",
            Fields =
            [
                new ReportTemplateFieldRequest
                {
                    Key = "progress_photos",
                    Label = "Progress Photos",
                    Type = ReportFieldType.Photos,
                    IsRequired = true,
                    Order = 1,
                    ModuleConfig = JsonDocument.Parse("""
                        {
                            "requiredViews": ["frontt"]
                        }
                        """).RootElement
                }
            ]
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Fields[0].ModuleConfig");
    }

    [Test]
    public async Task SubmitReportRequest_WithInvalidPhotoModuleConfig_ShouldReturnValidationError()
    {
        var traineeId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var templateId = Id<ReportTemplate>.New();
        var trainee = CreateUser(traineeId);
        var template = CreateTemplateWithPhotos(templateId, new[] { "Frontt" });
        var request = CreateReportRequest(requestId, traineeId, templateId, template);

        var service = CreateReportingService(findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request));

        var command = new SubmitReportRequestCommand
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["photos"] = JsonDocument.Parse("[]").RootElement
            }
        };

        var result = await service.SubmitReportRequestAsync(trainee, requestId, command);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidReportingError>();
        result.Error.Message.Should().Be(Messages.ReportFieldValidationFailed);
    }

    [Test]
    public void ScalarField_WithModuleConfig_ShouldHaveError()
    {
        var request = new UpsertReportTemplateRequest
        {
            Name = "Invalid Report",
            Fields =
            [
                new ReportTemplateFieldRequest
                {
                    Key = "feedback",
                    Label = "Feedback",
                    Type = ReportFieldType.Text,
                    IsRequired = true,
                    Order = 1,
                    ModuleConfig = JsonDocument.Parse("""
                        {
                            "someConfig": "value"
                        }
                        """).RootElement
                }
            ]
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Fields[0].ModuleConfig");
    }

    [Test]
    public void MixedTemplate_PhotosAndTextAndNumber_ValidatesCorrectly()
    {
        var request = new UpsertReportTemplateRequest
        {
            Name = "Complete Mixed Progress Report",
            Fields =
            [
                new ReportTemplateFieldRequest
                {
                    Key = "progress_photos",
                    Label = "Progress Photos",
                    Type = ReportFieldType.Photos,
                    IsRequired = true,
                    Order = 1,
                    ModuleConfig = JsonDocument.Parse("""
                        {
                            "requiredViews": ["front", "side", "back"]
                        }
                        """).RootElement
                },
                new ReportTemplateFieldRequest
                {
                    Key = "feedback_text",
                    Label = "Overall Feedback",
                    Type = ReportFieldType.Text,
                    IsRequired = false,
                    Order = 2,
                    ModuleConfig = null
                },
                new ReportTemplateFieldRequest
                {
                    Key = "current_weight",
                    Label = "Current Weight (kg)",
                    Type = ReportFieldType.Number,
                    IsRequired = true,
                    Order = 3,
                    ModuleConfig = null
                },
                new ReportTemplateFieldRequest
                {
                    Key = "training_completed",
                    Label = "Did you complete all sessions?",
                    Type = ReportFieldType.Boolean,
                    IsRequired = false,
                    Order = 4,
                    ModuleConfig = null
                }
            ]
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    #region Photo Validation Tests

    [Test]
    public async Task SubmitReportRequest_WithMissingOnePhotoView_ShouldReturnError()
    {
        var traineeId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var templateId = Id<ReportTemplate>.New();
        var trainee = CreateUser(traineeId);
        var template = CreateTemplateWithPhotos(templateId, new[] { "Front", "Side", "Back" });
        var request = CreateReportRequest(requestId, traineeId, templateId, template);

        var uploadedPhotos = new List<Photo>
        {
            CreatePhoto(Id<Photo>.New(), requestId, traineeId, PhotoViewType.Front),
            CreatePhoto(Id<Photo>.New(), requestId, traineeId, PhotoViewType.Side)
        };

        var service = CreateReportingService(
            findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request),
            getPhotosByRequestId: (_, _) => Task.FromResult(uploadedPhotos));

        var command = new SubmitReportRequestCommand
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["photos"] = JsonDocument.Parse("[]").RootElement
            }
        };

        var result = await service.SubmitReportRequestAsync(trainee, requestId, command);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidReportingError>();
        result.Error.Message.Should().Be(Messages.ReportFieldValidationFailed);
    }

    [Test]
    public async Task SubmitReportRequest_WithMissingAllPhotoViews_ShouldReturnError()
    {
        var traineeId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var templateId = Id<ReportTemplate>.New();
        var trainee = CreateUser(traineeId);
        var template = CreateTemplateWithPhotos(templateId, new[] { "Front", "Side", "Back" });
        var request = CreateReportRequest(requestId, traineeId, templateId, template);

        var uploadedPhotos = new List<Photo>();

        var service = CreateReportingService(
            findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request),
            getPhotosByRequestId: (_, _) => Task.FromResult(uploadedPhotos));

        var command = new SubmitReportRequestCommand
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["photos"] = JsonDocument.Parse("[]").RootElement
            }
        };

        var result = await service.SubmitReportRequestAsync(trainee, requestId, command);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidReportingError>();
        result.Error.Message.Should().Be(Messages.ReportFieldValidationFailed);
    }

    [Test]
    public async Task SubmitReportRequest_WithAllRequiredPhotoViews_ShouldSucceed()
    {
        var traineeId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var templateId = Id<ReportTemplate>.New();
        var trainee = CreateUser(traineeId);
        var template = CreateTemplateWithPhotos(templateId, new[] { "Front", "Side", "Back" });
        var request = CreateReportRequest(requestId, traineeId, templateId, template);

        var uploadedPhotos = new List<Photo>
        {
            CreatePhoto(Id<Photo>.New(), requestId, traineeId, PhotoViewType.Front),
            CreatePhoto(Id<Photo>.New(), requestId, traineeId, PhotoViewType.Side),
            CreatePhoto(Id<Photo>.New(), requestId, traineeId, PhotoViewType.Back)
        };

        var service = CreateReportingService(
            findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request),
            getPhotosByRequestId: (_, _) => Task.FromResult(uploadedPhotos),
            addSubmission: (_, _) => Task.CompletedTask);

        var command = new SubmitReportRequestCommand
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["photos"] = JsonDocument.Parse("[]").RootElement
            }
        };

        var result = await service.SubmitReportRequestAsync(trainee, requestId, command);

        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task SubmitReportRequest_WithNoPhotoFields_ShouldSucceedWithoutPhotoValidation()
    {
        var traineeId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var templateId = Id<ReportTemplate>.New();
        var trainee = CreateUser(traineeId);
        var template = CreateTemplateWithoutPhotos(templateId);
        var request = CreateReportRequest(requestId, traineeId, templateId, template);

        var service = CreateReportingService(
            findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request),
            addSubmission: (_, _) => Task.CompletedTask);

        var command = new SubmitReportRequestCommand
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["feedback"] = JsonDocument.Parse("\"Great progress\"").RootElement
            }
        };

        var result = await service.SubmitReportRequestAsync(trainee, requestId, command);

        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task SubmitReportRequest_WithExpiredUnsubmittedRequest_ShouldStillSucceed()
    {
        var traineeId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var templateId = Id<ReportTemplate>.New();
        var trainee = CreateUser(traineeId);
        var template = CreateTemplateWithoutPhotos(templateId);
        var request = CreateReportRequest(requestId, traineeId, templateId, template);
        request.Status = ReportRequestStatus.Expired;
        request.DueAt = DateTimeOffset.UtcNow.AddDays(-1);

        var service = CreateReportingService(
            findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request),
            addSubmission: (_, _) => Task.CompletedTask);

        var command = new SubmitReportRequestCommand
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["feedback"] = JsonDocument.Parse("\"Still submitting after due date\"").RootElement
            }
        };

        var result = await service.SubmitReportRequestAsync(trainee, requestId, command);

        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(ReportRequestStatus.Submitted);
        request.SubmittedAt.Should().NotBeNull();
    }

    #endregion

    #region Helper Methods

    private static User CreateUser(Id<User> userId)
    {
        return new User
        {
            Id = userId,
            Name = $"user_{userId}",
            Email = $"{userId}@example.com",
            ProfileRank = "Rookie"
        };
    }

    private static ReportTemplate CreateTemplateWithPhotos(Id<ReportTemplate> templateId, string[] requiredViews)
    {
        var config = new { requiredViews };
        return new ReportTemplate
        {
            Id = templateId,
            Name = "Photo Progress Report",
            TrainerId = Id<User>.New(),
            Fields =
            [
                new ReportTemplateField
                {
                    Id = Id<ReportTemplateField>.New(),
                    TemplateId = templateId,
                    Key = "photos",
                    Label = "Progress Photos",
                    Type = ReportFieldType.Photos,
                    IsRequired = true,
                    Order = 1,
                    ModuleConfig = JsonSerializer.Serialize(config)
                }
            ]
        };
    }

    private static ReportTemplate CreateTemplateWithoutPhotos(Id<ReportTemplate> templateId)
    {
        return new ReportTemplate
        {
            Id = templateId,
            Name = "Simple Report",
            TrainerId = Id<User>.New(),
            Fields =
            [
                new ReportTemplateField
                {
                    Id = Id<ReportTemplateField>.New(),
                    TemplateId = templateId,
                    Key = "feedback",
                    Label = "Feedback",
                    Type = ReportFieldType.Text,
                    IsRequired = true,
                    Order = 1
                }
            ]
        };
    }

    private static ReportRequest CreateReportRequest(
        Id<ReportRequest> requestId,
        Id<User> traineeId,
        Id<ReportTemplate> templateId,
        ReportTemplate template)
    {
        return new ReportRequest
        {
            Id = requestId,
            TraineeId = traineeId,
            TrainerId = Id<User>.New(),
            TemplateId = templateId,
            Template = template,
            Status = ReportRequestStatus.Pending
        };
    }

    private static Photo CreatePhoto(Id<Photo> photoId, Id<ReportRequest> requestId, Id<User> traineeId, PhotoViewType viewType)
    {
        return new Photo
        {
            Id = photoId,
            ReportRequestId = requestId,
            OwnerUserId = traineeId,
            UploaderUserId = traineeId,
            ViewType = viewType,
            StorageKey = $"photos/{traineeId}/{requestId}/{viewType}/photo.jpg",
            MimeType = "image/jpeg",
            SizeBytes = 1024,
            Checksum = "abc123",
            IsDeleted = false
        };
    }

    private static ReportingService CreateReportingService(
        Func<Id<ReportRequest>, CancellationToken, Task<ReportRequest?>>? findRequestById = null,
        Func<Id<ReportRequest>, CancellationToken, Task<List<Photo>>>? getPhotosByRequestId = null,
        Func<ReportSubmission, CancellationToken, Task>? addSubmission = null)
    {
        var repository = Substitute.For<IReportingRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var commandDispatcher = Substitute.For<ICommandDispatcher>();

        if (findRequestById != null)
        {
            repository.FindRequestByIdAsync(Arg.Any<Id<ReportRequest>>(), Arg.Any<CancellationToken>())
                .Returns(args => findRequestById((Id<ReportRequest>)args[0], (CancellationToken)args[1]));
        }

        if (getPhotosByRequestId != null)
        {
            repository.GetPhotosByRequestIdAsync(Arg.Any<Id<ReportRequest>>(), Arg.Any<CancellationToken>())
                .Returns(args => getPhotosByRequestId((Id<ReportRequest>)args[0], (CancellationToken)args[1]));
        }

        if (addSubmission != null)
        {
            repository.AddSubmissionAsync(Arg.Any<ReportSubmission>(), Arg.Any<CancellationToken>())
                .Returns(args => addSubmission((ReportSubmission)args[0], (CancellationToken)args[1]));
        }

        var uploadInitTracker = Substitute.For<IPhotoUploadInitTracker>();
        uploadInitTracker.CountRecentUploadInitsAsync(Arg.Any<Id<User>>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(0);
        var reportSubmissionMeasurementWriter = Substitute.For<IReportSubmissionMeasurementWriter>();

        var dependencies = Substitute.For<IReportingServiceDependencies>();
        dependencies.ReportingRepository.Returns(repository);
        dependencies.UnitOfWork.Returns(unitOfWork);
        dependencies.CommandDispatcher.Returns(commandDispatcher);
        dependencies.ReportSubmissionMeasurementWriter.Returns(reportSubmissionMeasurementWriter);
        dependencies.RoleRepository.Returns(Substitute.For<IRoleRepository>());
        dependencies.TrainerRelationshipRepository.Returns(Substitute.For<ITrainerRelationshipRepository>());
        dependencies.PhotoStorageProvider.Returns(Substitute.For<IPhotoStorageProvider>());
        dependencies.PhotoUploadInitTracker.Returns(uploadInitTracker);
        dependencies.Logger.Returns(Substitute.For<ILogger<ReportingService>>());
        dependencies.PhotoStorageOptions.Returns(new PhotoStorageOptions());

        return new ReportingService(dependencies);
    }

    #endregion
}
