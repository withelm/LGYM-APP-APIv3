using System.Net;
using System.Text.Json;
using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
using LgymApi.Resources;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ReportingServiceTests
{
    private ReportingService _service = null!;
    private FakeRoleRepository _roleRepository = null!;
    private FakeTrainerRelationshipRepository _trainerRelationshipRepository = null!;
    private FakeReportingRepository _reportingRepository = null!;
    private FakeUnitOfWork _unitOfWork = null!;

    [SetUp]
    public void SetUp()
    {
        _roleRepository = new FakeRoleRepository();
        _trainerRelationshipRepository = new FakeTrainerRelationshipRepository();
        _reportingRepository = new FakeReportingRepository();
        _unitOfWork = new FakeUnitOfWork();
        _service = new ReportingService(_roleRepository, _trainerRelationshipRepository, _reportingRepository, _unitOfWork);
    }

    [Test]
    public async Task CreateTemplateAsync_ThrowsBadRequest_WhenFieldKeyWhitespace()
    {
        var trainer = NewUser();

        var exception = Assert.ThrowsAsync<AppException>(async () =>
            await _service.CreateTemplateAsync(trainer, new CreateReportTemplateCommand
            {
                Name = "Weekly",
                Fields =
                [
                    new ReportTemplateFieldCommand { Key = " ", Label = "Weight", Type = ReportFieldType.Number, IsRequired = true, Order = 0 }
                ]
            }));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.StatusCode, Is.EqualTo((int)HttpStatusCode.BadRequest));
        Assert.That(exception.Message, Is.EqualTo(Messages.FieldRequired));
    }

    [Test]
    public async Task CreateTemplateAsync_CreatesTemplate_WithTrimmedValuesAndOrderedFields()
    {
        var trainer = NewUser();

        var result = await _service.CreateTemplateAsync(trainer, new CreateReportTemplateCommand
        {
            Name = "  Weekly Report  ",
            Description = "  Notes  ",
            Fields =
            [
                new ReportTemplateFieldCommand { Key = " bKey ", Label = " B ", Type = ReportFieldType.Text, IsRequired = false, Order = 2 },
                new ReportTemplateFieldCommand { Key = " aKey ", Label = " A ", Type = ReportFieldType.Number, IsRequired = true, Order = 1 }
            ]
        });

        Assert.That(result.Name, Is.EqualTo("Weekly Report"));
        Assert.That(result.Description, Is.EqualTo("Notes"));
        Assert.That(result.Fields.Select(f => f.Key).ToArray(), Is.EqualTo(new[] { "aKey", "bKey" }));
        Assert.That(_unitOfWork.SaveChangesCalls, Is.EqualTo(1));
    }

    [Test]
    public void CreateTemplateAsync_ThrowsForbidden_WhenUserIsNotTrainer()
    {
        var notTrainer = NewPlainUser();

        var exception = Assert.ThrowsAsync<AppException>(async () =>
            await _service.CreateTemplateAsync(notTrainer, new CreateReportTemplateCommand
            {
                Name = "Weekly",
                Fields = [new ReportTemplateFieldCommand { Key = "weight", Label = "Weight", Type = ReportFieldType.Number, IsRequired = true, Order = 0 }]
            }));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.StatusCode, Is.EqualTo((int)HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task GetPendingRequestsForTraineeAsync_ExpiresOverdueAndPersists()
    {
        var trainee = NewUser();
        var trainer = NewUser();
        var template = NewTemplate(trainer.Id, "T1", [NewField("weight", ReportFieldType.Number, true)]);

        _reportingRepository.Requests.Add(new ReportRequest
        {
            Id = Guid.NewGuid(),
            TrainerId = trainer.Id,
            TraineeId = trainee.Id,
            TemplateId = template.Id,
            Template = template,
            DueAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            Status = ReportRequestStatus.Pending
        });

        _reportingRepository.Requests.Add(new ReportRequest
        {
            Id = Guid.NewGuid(),
            TrainerId = trainer.Id,
            TraineeId = trainee.Id,
            TemplateId = template.Id,
            Template = template,
            DueAt = DateTimeOffset.UtcNow.AddMinutes(30),
            Status = ReportRequestStatus.Pending
        });

        var result = await _service.GetPendingRequestsForTraineeAsync(trainee);

        Assert.That(_unitOfWork.SaveChangesCalls, Is.EqualTo(1));
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(_reportingRepository.Requests.Count(r => r.Status == ReportRequestStatus.Expired), Is.EqualTo(1));
    }

    [Test]
    public async Task GetPendingRequestsForTraineeAsync_DoesNotSave_WhenNothingExpires()
    {
        var trainee = NewUser();
        var trainer = NewUser();
        var template = NewTemplate(trainer.Id, "T2", [NewField("sleep", ReportFieldType.Boolean, false)]);

        _reportingRepository.Requests.Add(new ReportRequest
        {
            Id = Guid.NewGuid(),
            TrainerId = trainer.Id,
            TraineeId = trainee.Id,
            TemplateId = template.Id,
            Template = template,
            DueAt = DateTimeOffset.UtcNow.AddDays(1),
            Status = ReportRequestStatus.Pending
        });

        var result = await _service.GetPendingRequestsForTraineeAsync(trainee);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(_unitOfWork.SaveChangesCalls, Is.EqualTo(0));
    }

    [Test]
    public async Task SubmitReportRequestAsync_AcceptsCaseInsensitiveKeys()
    {
        var trainee = NewUser();
        var trainer = NewUser();
        var template = NewTemplate(trainer.Id, "Weekly", [NewField("Weight", ReportFieldType.Number, true)]);
        var request = NewPendingRequest(trainer.Id, trainee.Id, template);
        _reportingRepository.Requests.Add(request);

        var result = await _service.SubmitReportRequestAsync(trainee, request.Id, new SubmitReportRequestCommand
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["weight"] = JsonSerializer.SerializeToElement(82)
            }
        });

        Assert.That(result.ReportRequestId, Is.EqualTo(request.Id));
        Assert.That(result.Answers.ContainsKey("WEIGHT"), Is.True);
        Assert.That(request.Status, Is.EqualTo(ReportRequestStatus.Submitted));
    }

    [Test]
    public async Task SubmitReportRequestAsync_AllowsEmptyAnswers_WhenAllFieldsOptional()
    {
        var trainee = NewUser();
        var trainer = NewUser();
        var template = NewTemplate(trainer.Id, "OptionalOnly", [NewField("notes", ReportFieldType.Text, false)]);
        var request = NewPendingRequest(trainer.Id, trainee.Id, template);
        _reportingRepository.Requests.Add(request);

        var result = await _service.SubmitReportRequestAsync(trainee, request.Id, new SubmitReportRequestCommand
        {
            Answers = new Dictionary<string, JsonElement>()
        });

        Assert.That(result.ReportRequestId, Is.EqualTo(request.Id));
        Assert.That(result.Answers, Is.Empty);
        Assert.That(request.Status, Is.EqualTo(ReportRequestStatus.Submitted));
    }

    [Test]
    public void SubmitReportRequestAsync_ThrowsBadRequest_WhenRequiredFieldMissing()
    {
        var trainee = NewUser();
        var trainer = NewUser();
        var template = NewTemplate(trainer.Id, "Required", [NewField("weight", ReportFieldType.Number, true)]);
        var request = NewPendingRequest(trainer.Id, trainee.Id, template);
        _reportingRepository.Requests.Add(request);

        var exception = Assert.ThrowsAsync<AppException>(async () =>
            await _service.SubmitReportRequestAsync(trainee, request.Id, new SubmitReportRequestCommand
            {
                Answers = new Dictionary<string, JsonElement>()
            }));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.StatusCode, Is.EqualTo((int)HttpStatusCode.BadRequest));
        Assert.That(exception.Message, Is.EqualTo(Messages.ReportFieldValidationFailed));
    }

    [Test]
    public async Task SubmitReportRequestAsync_AllowsNullForOptionalField()
    {
        var trainee = NewUser();
        var trainer = NewUser();
        var template = NewTemplate(trainer.Id, "Daily", [NewField("notes", ReportFieldType.Text, false)]);
        var request = NewPendingRequest(trainer.Id, trainee.Id, template);
        _reportingRepository.Requests.Add(request);

        var result = await _service.SubmitReportRequestAsync(trainee, request.Id, new SubmitReportRequestCommand
        {
            Answers = new Dictionary<string, JsonElement>
            {
                ["notes"] = JsonSerializer.SerializeToElement<string?>(null)
            }
        });

        Assert.That(result.ReportRequestId, Is.EqualTo(request.Id));
        Assert.That(request.Status, Is.EqualTo(ReportRequestStatus.Submitted));
    }

    [Test]
    public void SubmitReportRequestAsync_MapsDuplicateSubmissionToBadRequest()
    {
        var trainee = NewUser();
        var trainer = NewUser();
        var template = NewTemplate(trainer.Id, "Weekly", [NewField("weight", ReportFieldType.Number, true)]);
        var request = NewPendingRequest(trainer.Id, trainee.Id, template);
        _reportingRepository.Requests.Add(request);
        _unitOfWork.ThrowOnSave = new Exception("duplicate key value violates unique constraint ReportRequestId");

        var exception = Assert.ThrowsAsync<AppException>(async () =>
            await _service.SubmitReportRequestAsync(trainee, request.Id, new SubmitReportRequestCommand
            {
                Answers = new Dictionary<string, JsonElement>
                {
                    ["weight"] = JsonSerializer.SerializeToElement(80)
                }
            }));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.StatusCode, Is.EqualTo((int)HttpStatusCode.BadRequest));
        Assert.That(exception.Message, Is.EqualTo(Messages.ReportRequestNotPending));
    }

    [Test]
    public void SubmitReportRequestAsync_ThrowsNotFound_WhenRequestMissing()
    {
        var trainee = NewUser();

        var exception = Assert.ThrowsAsync<AppException>(async () =>
            await _service.SubmitReportRequestAsync(trainee, Guid.NewGuid(), new SubmitReportRequestCommand
            {
                Answers = new Dictionary<string, JsonElement>()
            }));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.StatusCode, Is.EqualTo((int)HttpStatusCode.NotFound));
    }

    [Test]
    public void SubmitReportRequestAsync_ThrowsBadRequest_WhenStatusNotPending()
    {
        var trainee = NewUser();
        var trainer = NewUser();
        var template = NewTemplate(trainer.Id, "Weekly", [NewField("weight", ReportFieldType.Number, true)]);
        var request = NewPendingRequest(trainer.Id, trainee.Id, template);
        request.Status = ReportRequestStatus.Submitted;
        _reportingRepository.Requests.Add(request);

        var exception = Assert.ThrowsAsync<AppException>(async () =>
            await _service.SubmitReportRequestAsync(trainee, request.Id, new SubmitReportRequestCommand
            {
                Answers = new Dictionary<string, JsonElement>
                {
                    ["weight"] = JsonSerializer.SerializeToElement(80)
                }
            }));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.StatusCode, Is.EqualTo((int)HttpStatusCode.BadRequest));
        Assert.That(exception.Message, Is.EqualTo(Messages.ReportRequestNotPending));
    }

    [Test]
    public void SubmitReportRequestAsync_ThrowsBadRequest_WhenExpired()
    {
        var trainee = NewUser();
        var trainer = NewUser();
        var template = NewTemplate(trainer.Id, "Weekly", [NewField("weight", ReportFieldType.Number, true)]);
        var request = NewPendingRequest(trainer.Id, trainee.Id, template);
        request.DueAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        _reportingRepository.Requests.Add(request);

        var exception = Assert.ThrowsAsync<AppException>(async () =>
            await _service.SubmitReportRequestAsync(trainee, request.Id, new SubmitReportRequestCommand
            {
                Answers = new Dictionary<string, JsonElement>
                {
                    ["weight"] = JsonSerializer.SerializeToElement(80)
                }
            }));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.StatusCode, Is.EqualTo((int)HttpStatusCode.BadRequest));
        Assert.That(request.Status, Is.EqualTo(ReportRequestStatus.Expired));
    }

    [Test]
    public async Task GetTraineeSubmissionsAsync_ReturnsMappedSubmissions()
    {
        var trainer = NewUser();
        var trainee = NewUser();
        var template = NewTemplate(trainer.Id, "Weekly", [NewField("weight", ReportFieldType.Number, true)]);
        var request = NewPendingRequest(trainer.Id, trainee.Id, template);

        _reportingRepository.Submissions.Add(new ReportSubmission
        {
            Id = Guid.NewGuid(),
            ReportRequestId = request.Id,
            TraineeId = trainee.Id,
            PayloadJson = "{\"weight\": 81}",
            ReportRequest = request
        });

        var result = await _service.GetTraineeSubmissionsAsync(trainer, trainee.Id);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Answers.ContainsKey("WEIGHT"), Is.True);
    }

    private User NewUser()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = $"u-{Guid.NewGuid():N}",
            Email = $"{Guid.NewGuid():N}@example.com"
        };

        _roleRepository.TrainerUserIds.Add(user.Id);
        return user;
    }

    private static User NewPlainUser()
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Name = $"u-{Guid.NewGuid():N}",
            Email = $"{Guid.NewGuid():N}@example.com"
        };
    }

    private static ReportTemplate NewTemplate(Guid trainerId, string name, IReadOnlyList<ReportTemplateField> fields)
    {
        return new ReportTemplate
        {
            Id = Guid.NewGuid(),
            TrainerId = trainerId,
            Name = name,
            Fields = fields.ToList()
        };
    }

    private static ReportTemplateField NewField(string key, ReportFieldType type, bool required)
    {
        return new ReportTemplateField
        {
            Id = Guid.NewGuid(),
            Key = key,
            Label = key,
            Type = type,
            IsRequired = required,
            Order = 0
        };
    }

    private ReportRequest NewPendingRequest(Guid trainerId, Guid traineeId, ReportTemplate template)
    {
        _trainerRelationshipRepository.Links[(trainerId, traineeId)] = new TrainerTraineeLink
        {
            Id = Guid.NewGuid(),
            TrainerId = trainerId,
            TraineeId = traineeId
        };

        return new ReportRequest
        {
            Id = Guid.NewGuid(),
            TrainerId = trainerId,
            TraineeId = traineeId,
            TemplateId = template.Id,
            Template = template,
            Status = ReportRequestStatus.Pending
        };
    }

    private sealed class FakeRoleRepository : IRoleRepository
    {
        public HashSet<Guid> TrainerUserIds { get; } = [];

        public Task<bool> UserHasRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
            => Task.FromResult(TrainerUserIds.Contains(userId) && roleName == AuthConstants.Roles.Trainer);

        public Task<List<Role>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<Role>());
        public Task<Role?> FindByIdAsync(Guid roleId, CancellationToken cancellationToken = default) => Task.FromResult<Role?>(null);
        public Task<Role?> FindByNameAsync(string roleName, CancellationToken cancellationToken = default) => Task.FromResult<Role?>(null);
        public Task<List<Role>> GetByNamesAsync(IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default) => Task.FromResult(new List<Role>());
        public Task<bool> ExistsByNameAsync(string roleName, Guid? excludeRoleId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<List<string>> GetRoleNamesByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(new List<string>());
        public Task<List<string>> GetPermissionClaimsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(new List<string>());
        public Task<List<string>> GetPermissionClaimsByRoleIdAsync(Guid roleId, CancellationToken cancellationToken = default) => Task.FromResult(new List<string>());
        public Task<Dictionary<Guid, List<string>>> GetPermissionClaimsByRoleIdsAsync(IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default) => Task.FromResult(new Dictionary<Guid, List<string>>());
        public Task<bool> UserHasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddRoleAsync(Role role, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateRoleAsync(Role role, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteRoleAsync(Role role, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReplaceRolePermissionClaimsAsync(Guid roleId, IReadOnlyCollection<string> permissionClaims, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddUserRolesAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReplaceUserRolesAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeTrainerRelationshipRepository : ITrainerRelationshipRepository
    {
        public Dictionary<(Guid TrainerId, Guid TraineeId), TrainerTraineeLink> Links { get; } = new();

        public Task<TrainerTraineeLink?> FindActiveLinkByTrainerAndTraineeAsync(Guid trainerId, Guid traineeId, CancellationToken cancellationToken = default)
            => Task.FromResult(Links.TryGetValue((trainerId, traineeId), out var link) ? link : null);

        public Task AddInvitationAsync(TrainerInvitation invitation, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TrainerInvitation?> FindInvitationByIdAsync(Guid invitationId, CancellationToken cancellationToken = default) => Task.FromResult<TrainerInvitation?>(null);
        public Task<TrainerInvitation?> FindPendingInvitationAsync(Guid trainerId, Guid traineeId, CancellationToken cancellationToken = default) => Task.FromResult<TrainerInvitation?>(null);
        public Task<List<TrainerInvitation>> GetInvitationsByTrainerIdAsync(Guid trainerId, CancellationToken cancellationToken = default) => Task.FromResult(new List<TrainerInvitation>());
        public Task<bool> HasActiveLinkForTraineeAsync(Guid traineeId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<TrainerTraineeLink?> FindActiveLinkByTraineeIdAsync(Guid traineeId, CancellationToken cancellationToken = default) => Task.FromResult<TrainerTraineeLink?>(null);
        public Task<LgymApi.Application.Features.TrainerRelationships.Models.TrainerDashboardTraineeListResult> GetDashboardTraineesAsync(Guid trainerId, LgymApi.Application.Features.TrainerRelationships.Models.TrainerDashboardTraineeQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(new LgymApi.Application.Features.TrainerRelationships.Models.TrainerDashboardTraineeListResult());
        public Task AddLinkAsync(TrainerTraineeLink link, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveLinkAsync(TrainerTraineeLink link, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeReportingRepository : IReportingRepository
    {
        public List<ReportTemplate> Templates { get; } = [];
        public List<ReportRequest> Requests { get; } = [];
        public List<ReportSubmission> Submissions { get; } = [];

        public Task AddTemplateAsync(ReportTemplate template, CancellationToken cancellationToken = default)
        {
            Templates.Add(template);
            return Task.CompletedTask;
        }

        public Task<ReportTemplate?> FindTemplateByIdAsync(Guid templateId, CancellationToken cancellationToken = default)
            => Task.FromResult(Templates.FirstOrDefault(x => x.Id == templateId));

        public Task<List<ReportTemplate>> GetTemplatesByTrainerIdAsync(Guid trainerId, CancellationToken cancellationToken = default)
            => Task.FromResult(Templates.Where(x => x.TrainerId == trainerId && !x.IsDeleted).ToList());

        public Task AddRequestAsync(ReportRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }

        public Task<ReportRequest?> FindRequestByIdAsync(Guid requestId, CancellationToken cancellationToken = default)
            => Task.FromResult(Requests.FirstOrDefault(x => x.Id == requestId));

        public Task<List<ReportRequest>> GetPendingRequestsByTraineeIdAsync(Guid traineeId, CancellationToken cancellationToken = default)
            => Task.FromResult(Requests
                .Where(x => x.TraineeId == traineeId && x.Status == ReportRequestStatus.Pending)
                .OrderByDescending(x => x.CreatedAt)
                .ToList());

        public Task AddSubmissionAsync(ReportSubmission submission, CancellationToken cancellationToken = default)
        {
            Submissions.Add(submission);
            return Task.CompletedTask;
        }

        public Task<List<ReportSubmission>> GetSubmissionsByTrainerAndTraineeAsync(Guid trainerId, Guid traineeId, CancellationToken cancellationToken = default)
            => Task.FromResult(Submissions.Where(x => x.TraineeId == traineeId && x.ReportRequest.TrainerId == trainerId).ToList());
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }
        public Exception? ThrowOnSave { get; set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowOnSave != null)
            {
                throw ThrowOnSave;
            }

            SaveChangesCalls++;
            return Task.FromResult(1);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IUnitOfWorkTransaction>(new FakeTransaction());
    }

    private sealed class FakeTransaction : IUnitOfWorkTransaction
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
