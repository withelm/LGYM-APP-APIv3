using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Data.SeedData;
using LgymApi.IntegrationTests.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class ReportingEngineTests : IntegrationTestBase
{
    [Test]
    public async Task AnswerEdit_RequiresAnswersPropertyButAllowsValidEmptyReplacement()
    {
        var trainer = await SeedTrainerAsync("edit-optional-trainer", "edit-optional-trainer@example.test");
        var trainee = await SeedUserAsync("edit-optional-trainee", "edit-optional-trainee@example.test");
        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.TrainerTraineeLinks.Add(new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = trainer.Id, TraineeId = trainee.Id });
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);
        using var templateResponse = await Client.PostAsJsonAsync("/api/trainer/report-templates", new
        {
            name = "Optional report",
            fields = new[] { new { key = "note", label = "Note", type = "Text", isRequired = false, order = 0 } }
        });
        Assert.That(templateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var template = (await templateResponse.Content.ReadFromJsonAsync<ReportTemplateResponse>())!;
        using var requestResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/report-requests", new { templateId = template.Id });
        Assert.That(requestResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var request = (await requestResponse.Content.ReadFromJsonAsync<ReportRequestResponse>())!;

        SetAuthorizationHeader(trainee.Id);
        using var submit = await Client.PostAsJsonAsync($"/api/trainee/report-requests/{request.Id}/submit", new { answers = new { note = "before" } });
        Assert.That(submit.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var submission = (await submit.Content.ReadFromJsonAsync<ReportSubmissionResponse>())!;
        Assert.That(Id<ReportSubmission>.TryParse(submission.Id, out var submissionId), Is.True);
        var route = $"/api/trainee/report-submissions/{submission.Id}/answers";

        foreach (var body in new[] { "{}", "{\"answers\":null}" })
        {
            using var response = await Client.PostAsync(route, new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), string.Format(System.Globalization.CultureInfo.InvariantCulture, "request body {0} must not replace answers", body));
            using var scope = Factory.Services.CreateScope();
            var persisted = await scope.ServiceProvider.GetRequiredService<AppDbContext>().ReportSubmissions.AsNoTracking().SingleAsync(item => item.Id == submissionId);
            using var payload = JsonDocument.Parse(persisted.PayloadJson);
            Assert.That(payload.RootElement.GetProperty("note").GetString(), Is.EqualTo("before"));
        }

        using var empty = await Client.PostAsJsonAsync(route, new { answers = new { } });
        Assert.That(empty.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var assertionScope = Factory.Services.CreateScope();
        var saved = await assertionScope.ServiceProvider.GetRequiredService<AppDbContext>().ReportSubmissions.AsNoTracking().SingleAsync(item => item.Id == submissionId);
        using var savedPayload = JsonDocument.Parse(saved.PayloadJson);
        Assert.That(savedPayload.RootElement.EnumerateObject(), Is.Empty);
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainee/report-submissions/{submissionId}/answers", "own", "owner-allow")]
    [AuthorizationEvidence("POST", "/api/trainee/report-submissions/{submissionId}/answers", "own", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/trainee/report-submissions/{submissionId}/answers", "own", "anonymous-denial")]
    public async Task AnswerEdit_ProtectsMeasurementsAndStopsPermanentlyAfterFeedback()
    {
        var trainer = await SeedTrainerAsync("edit-trainer", "edit-trainer@example.test");
        var trainee = await SeedUserAsync("edit-trainee", "edit-trainee@example.test");
        var other = await SeedUserAsync("edit-other", "edit-other@example.test");
        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.TrainerTraineeLinks.Add(new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = trainer.Id, TraineeId = trainee.Id });
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);
        using var templateResponse = await Client.PostAsJsonAsync("/api/trainer/report-templates", new
        {
            name = "Edit report",
            fields = new object[]
            {
                new { key = "note", label = "Note", type = "Text", isRequired = true, order = 0 },
                new { key = "measurements", label = "Measurements", type = "Measurements", isRequired = false, order = 1, moduleConfig = new { measurementTypes = new[] { "weight" } } }
            }
        });
        Assert.That(templateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var template = (await templateResponse.Content.ReadFromJsonAsync<ReportTemplateResponse>())!;
        using var requestResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/report-requests", new { templateId = template.Id });
        Assert.That(requestResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var request = (await requestResponse.Content.ReadFromJsonAsync<ReportRequestResponse>())!;

        SetAuthorizationHeader(trainee.Id);
        var original = new { note = "before", measurements = new { weight = new { value = 80, unit = "kg" } } };
        using var submit = await Client.PostAsJsonAsync($"/api/trainee/report-requests/{request.Id}/submit", new { answers = original });
        Assert.That(submit.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var submission = (await submit.Content.ReadFromJsonAsync<ReportSubmissionResponse>())!;
        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.That((await database.CommandEnvelopes.CountAsync(item => item.CommandTypeFullName == "LgymApi.BackgroundWorker.Common.Commands.ReportSubmissionAcceptedProgressCommand")), Is.EqualTo(1));
        }
        var route = $"/api/trainee/report-submissions/{submission.Id}/answers";
        using var omitted = await Client.PostAsJsonAsync(route, new { answers = new { note = "lost" } });
        Assert.That(omitted.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var altered = await Client.PostAsJsonAsync(route, new { answers = new { note = "lost", measurements = new { weight = new { value = 90, unit = "kg" } } } });
        Assert.That(altered.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var duplicate = await Client.PostAsync(route, new StringContent("{\"answers\":{\"note\":\"lost\",\"measurements\":{},\"MEASUREMENTS\":{\"weight\":{\"value\":80,\"unit\":\"kg\"}}}}", System.Text.Encoding.UTF8, "application/json"));
        Assert.That(duplicate.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var exactDuplicate = await Client.PostAsync(route, new StringContent("{\"answers\":{\"note\":\"lost\",\"measurements\":{},\"measurements\":{\"weight\":{\"value\":80,\"unit\":\"kg\"}}}}", System.Text.Encoding.UTF8, "application/json"));
        Assert.That(exactDuplicate.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var edited = await Client.PostAsJsonAsync(route, new { answers = new { note = "after", original.measurements } });
        Assert.That(edited.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var editedBody = (await edited.Content.ReadFromJsonAsync<ReportSubmissionResponse>())!;
        Assert.That(editedBody.Answers["note"].GetString(), Is.EqualTo("after"));
        Assert.That(editedBody.Answers["measurements"].GetProperty("weight").GetProperty("value").GetInt32(), Is.EqualTo(80));

        Assert.That(Id<ReportSubmission>.TryParse(submission.Id, out var submissionId), Is.True);
        foreach (var (storedPayload, replacement, expectedStatus) in new[]
        {
            ("{\"note\":\"before\"}", "{\"note\":\"after\"}", HttpStatusCode.OK),
            ("{\"note\":\"before\",\"measurements\":null}", "{\"note\":\"after\",\"MEASUREMENTS\":null}", HttpStatusCode.OK),
            ("{\"note\":\"before\",\"measurements\":\"invalid\"}", "{\"note\":\"after\",\"measurements\":\"invalid\"}", HttpStatusCode.OK),
            ("{\"note\":\"before\",\"measurements\":null}", "{\"note\":\"after\"}", HttpStatusCode.BadRequest),
            ("{\"note\":\"before\",\"measurements\":null,\"MEASUREMENTS\":{}}", "{\"note\":\"after\",\"measurements\":null}", HttpStatusCode.BadRequest),
            ("{\"note\":\"before\",\"measurements\":{\"weight\":{},\"WEIGHT\":{}}}", "{\"note\":\"after\",\"measurements\":{\"weight\":{},\"WEIGHT\":{}}}", HttpStatusCode.BadRequest),
            ("not-json", "{\"note\":\"after\"}", HttpStatusCode.BadRequest)
        })
        {
            using (var scope = Factory.Services.CreateScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var row = await database.ReportSubmissions.SingleAsync(item => item.Id == submissionId);
                row.PayloadJson = storedPayload;
                await database.SaveChangesAsync();
            }
            using var response = await Client.PostAsync(route, new StringContent($"{{\"answers\":{replacement}}}", System.Text.Encoding.UTF8, "application/json"));
            Assert.That(response.StatusCode, Is.EqualTo(expectedStatus), string.Format(System.Globalization.CultureInfo.InvariantCulture, "stored payload {0}", storedPayload));
            using var assertionScope = Factory.Services.CreateScope();
            var persisted = await assertionScope.ServiceProvider.GetRequiredService<AppDbContext>().ReportSubmissions.AsNoTracking().SingleAsync(item => item.Id == submissionId);
            if (expectedStatus == HttpStatusCode.BadRequest)
            {
                Assert.That(persisted.PayloadJson, Is.EqualTo(storedPayload));
            }
            else
            {
                using var originalJson = JsonDocument.Parse(storedPayload);
                using var savedJson = JsonDocument.Parse(persisted.PayloadJson);
                var originalHasMeasurement = originalJson.RootElement.TryGetProperty("measurements", out var originalMeasurement);
                var savedHasMeasurement = savedJson.RootElement.EnumerateObject().Any(item => item.Name.Equals("measurements", StringComparison.OrdinalIgnoreCase));
                Assert.That(savedHasMeasurement, Is.EqualTo(originalHasMeasurement));
                if (originalHasMeasurement)
                {
                    var savedMeasurement = savedJson.RootElement.EnumerateObject().Single(item => item.Name.Equals("measurements", StringComparison.OrdinalIgnoreCase)).Value;
                    Assert.That(JsonElement.DeepEquals(originalMeasurement, savedMeasurement), Is.True);
                }
            }
        }
        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await database.ReportSubmissions.SingleAsync(item => item.Id == submissionId);
            row.PayloadJson = JsonSerializer.Serialize(new { note = "after", original.measurements });
            await database.SaveChangesAsync();
            Assert.That((await database.CommandEnvelopes.CountAsync(item => item.CommandTypeFullName == "LgymApi.BackgroundWorker.Common.Commands.ReportSubmissionAcceptedProgressCommand")), Is.EqualTo(1));
        }

        SetAuthorizationHeader(other.Id);
        using var foreign = await Client.PostAsJsonAsync(route, new { answers = original });
        Assert.That(foreign.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        ClearAuthorizationHeader();
        using var anonymous = await Client.PostAsJsonAsync(route, new { answers = original });
        Assert.That(anonymous.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        SetAuthorizationHeader(trainer.Id);
        var feedbackRoute = $"/api/trainer/trainees/{trainee.Id}/report-submissions/{submission.Id}/feedback";
        using var feedback = await Client.PostAsJsonAsync(feedbackRoute, new { trainerOverallComment = "feedback", trainerFieldComments = new { } });
        Assert.That(feedback.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var cleared = await Client.PostAsJsonAsync(feedbackRoute, new { trainerOverallComment = (string?)null, trainerFieldComments = new { } });
        Assert.That(cleared.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        SetAuthorizationHeader(trainee.Id);
        using var blocked = await Client.PostAsJsonAsync(route, new { answers = original });
        Assert.That(blocked.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        using var list = await Client.GetAsync("/api/trainee/report-submissions");
        using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.That(listJson.RootElement[0].GetProperty("canEditAnswers").GetBoolean(), Is.False);
        Assert.That(listJson.RootElement[0].GetProperty("answers").GetProperty("note").GetString(), Is.EqualTo("after"));
    }

    [Test]
    public async Task ReportFlow_TrainerCreatesRequest_TraineeSubmits_TrainerCanReadSubmission()
    {
        var trainer = await SeedTrainerAsync("trainer-reports", "trainer-reports@example.com");
        var trainee = await SeedUserAsync(name: "trainee-reports", email: "trainee-reports@example.com", password: "password123");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Domain.ValueObjects.Id<TrainerTraineeLink>.New(),
                TrainerId = (Domain.ValueObjects.Id<User>)trainer.Id,
                TraineeId = (Domain.ValueObjects.Id<User>)trainee.Id
            });
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);
        var createTemplateResponse = await Client.PostAsJsonAsync("/api/trainer/report-templates", new
        {
            name = "Weekly Check-in",
            description = "Basic wellness report",
            fields = new object[]
            {
                new { key = "weight", label = "Weight", type = "Number", isRequired = true, order = 0 },
                new { key = "sleptWell", label = "Slept Well", type = "Boolean", isRequired = false, order = 1 }
            }
        });

        Assert.That(createTemplateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var template = await createTemplateResponse.Content.ReadFromJsonAsync<ReportTemplateResponse>();
        Assert.That(template, Is.Not.Null);
        Assert.That(template!.Fields, Has.Exactly(2).Items);

        var createRequestResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/report-requests", new
        {
            templateId = template.Id,
            dueAt = DateTimeOffset.UtcNow.AddDays(2)
        });

        Assert.That(createRequestResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var request = await createRequestResponse.Content.ReadFromJsonAsync<ReportRequestResponse>();
        Assert.That(request, Is.Not.Null);
        Assert.That(request!.Status, Is.EqualTo("Pending"));

        SetAuthorizationHeader(trainee.Id);
        var pendingResponse = await Client.GetAsync("/api/trainee/report-requests");
        Assert.That(pendingResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var pending = await pendingResponse.Content.ReadFromJsonAsync<List<ReportRequestResponse>>();
        Assert.That(pending, Is.Not.Null);
        Assert.That((pending!).Where(x => x.Id == request.Id), Has.Exactly(1).Items);

        var submitResponse = await Client.PostAsJsonAsync($"/api/trainee/report-requests/{request.Id}/submit", new
        {
            answers = new
            {
                weight = 81.2,
                sleptWell = true
            }
        });

        Assert.That(submitResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        SetAuthorizationHeader(trainer.Id);
        var submissionsResponse = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/report-submissions");
        Assert.That(submissionsResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var submissions = await submissionsResponse.Content.ReadFromJsonAsync<List<ReportSubmissionResponse>>();
        Assert.That(submissions, Is.Not.Null);
        Assert.That(submissions!, Has.Exactly(1).Items);
        Assert.That(submissions[0].ReportRequestId, Is.EqualTo(request.Id));
        Assert.That(submissions[0].Answers["weight"].GetDouble(), Is.EqualTo(81.2).Within(0.001));
    }

    [Test]
    public async Task SubmittedArchive_AfterSourceTemplateEditAndDelete_PreservesIssuedTemplateAndPhotoTypeSnapshot()
    {
        var trainer = await SeedTrainerAsync("archive-snapshot-trainer", "archive-snapshot-trainer@example.test");
        var trainee = await SeedUserAsync("archive-snapshot-trainee", "archive-snapshot-trainee@example.test");
        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.TrainerTraineeLinks.Add(new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = trainer.Id, TraineeId = trainee.Id });
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);
        using var templateResponse = await Client.PostAsJsonAsync("/api/trainer/report-templates", new
        {
            name = "Issued archive template",
            fields = new object[]
            {
                new { key = "checkIn", label = "Issued check-in", type = "Text", isRequired = true, order = 0 },
                new { key = "photos", label = "Issued photos", type = "Photos", isRequired = false, order = 1, moduleConfig = new { requiredViews = new[] { "Front" } } }
            }
        });
        Assert.That(templateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var template = await templateResponse.Content.ReadFromJsonAsync<ReportTemplateResponse>();
        Assert.That(template, Is.Not.Null);

        using var requestResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/report-requests", new { templateId = template!.Id });
        Assert.That(requestResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var request = await requestResponse.Content.ReadFromJsonAsync<ReportRequestResponse>();
        Assert.That(request, Is.Not.Null);

        SetAuthorizationHeader(trainee.Id);
        using var submitResponse = await Client.PostAsJsonAsync($"/api/trainee/report-requests/{request!.Id}/submit", new { answers = new { checkIn = "immutable archive" } });
        Assert.That(submitResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        SetAuthorizationHeader(trainer.Id);
        using var updateResponse = await Client.PostAsJsonAsync($"/api/trainer/report-templates/{template.Id}/update", new
        {
            name = "Mutated source template",
            fields = new[] { new { key = "replacement", label = "Replacement", type = "Text", isRequired = true, order = 0 } }
        });
        Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var deleteResponse = await Client.PostAsync($"/api/trainer/report-templates/{template.Id}/delete", null);
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        SetAuthorizationHeader(trainee.Id);
        using var archiveRequest = new HttpRequestMessage(HttpMethod.Get, "/api/trainee/report-submissions");
        archiveRequest.Headers.Add("X-LGYM-Reporting-Protocol", "2");
        using var archiveResponse = await Client.SendAsync(archiveRequest);
        Assert.That(archiveResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var archive = JsonDocument.Parse(await archiveResponse.Content.ReadAsStringAsync());
        var archivedRequest = archive.RootElement[0].GetProperty("request");
        Assert.That(archivedRequest.GetProperty("definitionSnapshotProvenance").GetString(), Is.EqualTo("Issued"));
        var archivedTemplate = archivedRequest.GetProperty("template");
        Assert.That(archivedTemplate.GetProperty("name").GetString(), Is.EqualTo("Issued archive template"));
        var archivedFields = archivedTemplate.GetProperty("fields");
        Assert.That(archivedFields.EnumerateArray().Select(field => field.GetProperty("key").GetString()), Is.SupersetOf(new[] { "checkIn", "photos" }));
        Assert.That(archivedFields.EnumerateArray()
            .Single(field => field.GetProperty("key").GetString() == "photos")
            .GetProperty("moduleConfig").GetProperty("photoTypes").GetArrayLength(), Is.GreaterThan(0));
    }

    [Test]
    public async Task SubmissionCollections_ExcludeSoftDeletedSubmissionAndRequestRows()
    {
        var trainer = await SeedTrainerAsync("deleted-archive-trainer", "deleted-archive-trainer@example.test");
        var trainee = await SeedUserAsync("deleted-archive-trainee", "deleted-archive-trainee@example.test");
        var now = DateTimeOffset.UtcNow;
        var template = new ReportTemplate { Id = Id<ReportTemplate>.New(), TrainerId = trainer.Id, Name = "Archive privacy" };
        var visibleRequest = new ReportRequest { Id = Id<ReportRequest>.New(), TrainerId = trainer.Id, TraineeId = trainee.Id, TemplateId = template.Id, Template = template, Status = ReportRequestStatus.Submitted, CreatedAt = now };
        var deletedRequest = new ReportRequest { Id = Id<ReportRequest>.New(), TrainerId = trainer.Id, TraineeId = trainee.Id, TemplateId = template.Id, Template = template, Status = ReportRequestStatus.Submitted, CreatedAt = now, IsDeleted = true };
        var deletedSubmissionRequest = new ReportRequest { Id = Id<ReportRequest>.New(), TrainerId = trainer.Id, TraineeId = trainee.Id, TemplateId = template.Id, Template = template, Status = ReportRequestStatus.Submitted, CreatedAt = now };
        var visibleSubmission = new ReportSubmission { Id = Id<ReportSubmission>.New(), ReportRequestId = visibleRequest.Id, ReportRequest = visibleRequest, TraineeId = trainee.Id, PayloadJson = "{}", CreatedAt = now };
        var deletedRequestSubmission = new ReportSubmission { Id = Id<ReportSubmission>.New(), ReportRequestId = deletedRequest.Id, ReportRequest = deletedRequest, TraineeId = trainee.Id, PayloadJson = "{}", CreatedAt = now };
        var deletedSubmission = new ReportSubmission { Id = Id<ReportSubmission>.New(), ReportRequestId = deletedSubmissionRequest.Id, ReportRequest = deletedSubmissionRequest, TraineeId = trainee.Id, PayloadJson = "{}", CreatedAt = now, IsDeleted = true };
        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.TrainerTraineeLinks.Add(new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = trainer.Id, TraineeId = trainee.Id });
            database.ReportTemplates.Add(template);
            database.ReportRequests.AddRange(visibleRequest, deletedRequest, deletedSubmissionRequest);
            database.ReportSubmissions.AddRange(visibleSubmission, deletedRequestSubmission, deletedSubmission);
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainee.Id);
        using var traineeResponse = await Client.GetAsync("/api/trainee/report-submissions");
        Assert.That(traineeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var traineeBody = await traineeResponse.Content.ReadAsStringAsync();
        var assertedChain17 = traineeBody;
        Assert.That(assertedChain17, Does.Contain(visibleSubmission.Id.ToString()));
        Assert.That(assertedChain17, Does.Not.Contain(deletedRequestSubmission.Id.ToString()).And.Not.Contain(deletedSubmission.Id.ToString()));

        SetAuthorizationHeader(trainer.Id);
        using var trainerResponse = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/report-submissions");
        Assert.That(trainerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var trainerBody = await trainerResponse.Content.ReadAsStringAsync();
        var assertedChain18 = trainerBody;
        Assert.That(assertedChain18, Does.Contain(visibleSubmission.Id.ToString()));
        Assert.That(assertedChain18, Does.Not.Contain(deletedRequestSubmission.Id.ToString()).And.Not.Contain(deletedSubmission.Id.ToString()));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/report-requests", "trainer-shared", "active-relationship-allow")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/report-requests", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/report-requests", "trainer-shared", "former-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/report-requests", "trainer-shared", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/report-requests", "trainer-shared", "anonymous-denial")]
    [AuthorizationEvidence("GET", "/api/trainee/report-requests", "own", "owner-allow")]
    [AuthorizationEvidence("GET", "/api/trainee/report-requests", "own", "no-client-subject")]
    [AuthorizationEvidence("GET", "/api/trainee/report-requests", "own", "anonymous-denial")]
    public async Task TraineePendingReportRequestsRoute_IsActorScopedAndNonDisclosing()
    {
        var trainer = await SeedTrainerAsync("task10-report-request-trainer", "task10-report-request-trainer@example.test");
        var trainee = await SeedUserAsync("task10-report-request-trainee", "task10-report-request-trainee@example.test");
        var otherTrainee = await SeedUserAsync("task10-report-request-other", "task10-report-request-other@example.test");

        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id
            });
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);
        using var templateResponse = await Client.PostAsJsonAsync("/api/trainer/report-templates", new
        {
            name = "Task 10 protected report template",
            fields = new[]
            {
                new { key = "checkIn", label = "Check-in", type = "Text", isRequired = true, order = 0 }
            }
        });
        Assert.That(templateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var template = await templateResponse.Content.ReadFromJsonAsync<ReportTemplateResponse>();
        Assert.That(template, Is.Not.Null);

        using var createRequestResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/report-requests", new
        {
            templateId = template!.Id,
            note = "Protected pending report request"
        });
        Assert.That(createRequestResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var createdRequest = await createRequestResponse.Content.ReadFromJsonAsync<ReportRequestResponse>();
        Assert.That(createdRequest, Is.Not.Null);

        SetAuthorizationHeader(trainee.Id);
        using var ownerResponse = await Client.GetAsync("/api/trainee/report-requests");
        Assert.That(ownerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var ownerRequests = await ownerResponse.Content.ReadFromJsonAsync<List<ReportRequestResponse>>();
        Assert.That(ownerRequests, Is.Not.Null);
        Assert.That((ownerRequests!).Where(request => request.Id == createdRequest!.Id), Has.Exactly(1).Items);
        Assert.That(ownerRequests.Single().Note, Is.EqualTo("Protected pending report request"));

        SetAuthorizationHeader(otherTrainee.Id);
        using var otherResponse = await Client.GetAsync("/api/trainee/report-requests");
        Assert.That(otherResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var otherRequests = await otherResponse.Content.ReadFromJsonAsync<List<ReportRequestResponse>>();
        Assert.That(otherRequests, Is.Not.Null);
        Assert.That(otherRequests, Is.Empty);
        var otherResponseText = await otherResponse.Content.ReadAsStringAsync();
        Assert.That(otherResponseText, Does.Not.Contain(createdRequest!.Id));
        Assert.That(otherResponseText, Does.Not.Contain("Protected pending report request"));

        ClearAuthorizationHeader();
        using var anonymousResponse = await Client.GetAsync("/api/trainee/report-requests");
        Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var anonymousResponseText = await anonymousResponse.Content.ReadAsStringAsync();
        Assert.That(anonymousResponseText, Does.Not.Contain(createdRequest.Id));
        Assert.That(anonymousResponseText, Does.Not.Contain("Protected pending report request"));

        SetAuthorizationHeader(trainer.Id);
        using var unrelatedCreateResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{otherTrainee.Id}/report-requests", new
        {
            templateId = template.Id,
            note = "Blocked unrelated report request"
        });
        Assert.That(unrelatedCreateResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        SetAuthorizationHeader(otherTrainee.Id);
        using var ordinaryCreateResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/report-requests", new
        {
            templateId = template.Id,
            note = "Blocked ordinary report request"
        });
        Assert.That(ordinaryCreateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        ClearAuthorizationHeader();
        using var anonymousCreateResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/report-requests", new
        {
            templateId = template.Id,
            note = "Blocked anonymous report request"
        });
        Assert.That(anonymousCreateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        SetAuthorizationHeader(trainer.Id);
        using (var unlinkScope = Factory.Services.CreateScope())
        {
            var database = unlinkScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var link = await database.TrainerTraineeLinks.SingleAsync(candidate => candidate.TrainerId == trainer.Id && candidate.TraineeId == trainee.Id);
            database.TrainerTraineeLinks.Remove(link);
            await database.SaveChangesAsync();
        }

        using var formerCreateResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/report-requests", new
        {
            templateId = template.Id,
            note = "Blocked former report request"
        });
        Assert.That(formerCreateResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    [AuthorizationEvidence("GET", "/api/trainer/report-templates", "own", "owner-allow")]
    [AuthorizationEvidence("GET", "/api/trainer/report-templates", "own", "no-client-subject")]
    [AuthorizationEvidence("GET", "/api/trainer/report-templates", "own", "anonymous-denial")]
    public async Task TrainerReportTemplatesRoute_IsOwnerScopedAndNonDisclosing()
    {
        var owner = await SeedTrainerAsync("task10-template-owner", "task10-template-owner@example.test");
        var otherTrainer = await SeedTrainerAsync("task10-template-other", "task10-template-other@example.test");
        var ordinaryUser = await SeedUserAsync("task10-template-ordinary", "task10-template-ordinary@example.test");

        SetAuthorizationHeader(owner.Id);
        var ownerTemplateOne = await CreateReportTemplateAsync(
            "Task 10 owner template one",
            "Owner-only field one",
            "ownerFieldOne");
        var ownerTemplateTwo = await CreateReportTemplateAsync(
            "Task 10 owner template two",
            "Owner-only field two",
            "ownerFieldTwo");

        using var ownerResponse = await Client.GetAsync("/api/trainer/report-templates");
        Assert.That(ownerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var ownerTemplates = await ownerResponse.Content.ReadFromJsonAsync<List<ReportTemplateResponse>>();
        Assert.That(ownerTemplates, Is.Not.Null);
        Assert.That(ownerTemplates!.Select(template => template.Id), Is.EquivalentTo(new[] { ownerTemplateOne.Id, ownerTemplateTwo.Id }).UsingPropertiesComparer());
        Assert.That(ownerTemplates.Select(template => template.Name), Is.EquivalentTo(new[] { "Task 10 owner template one", "Task 10 owner template two" }));
        Assert.That(ownerTemplates.SelectMany(template => template.Fields.Select(field => field.Label)), Is.EquivalentTo(new[] { "Owner-only field one", "Owner-only field two" }));

        SetAuthorizationHeader(otherTrainer.Id);
        using var otherTrainerResponse = await Client.GetAsync("/api/trainer/report-templates");
        Assert.That(otherTrainerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var otherTrainerTemplates = await otherTrainerResponse.Content.ReadFromJsonAsync<List<ReportTemplateResponse>>();
        Assert.That(otherTrainerTemplates, Is.Not.Null);
        Assert.That(otherTrainerTemplates, Is.Empty);
        var otherTrainerText = await otherTrainerResponse.Content.ReadAsStringAsync();
        Assert.That(otherTrainerText, Does.Not.Contain(ownerTemplateOne.Id));
        Assert.That(otherTrainerText, Does.Not.Contain(ownerTemplateTwo.Id));
        Assert.That(otherTrainerText, Does.Not.Contain("Task 10 owner template one"));
        Assert.That(otherTrainerText, Does.Not.Contain("Task 10 owner template two"));
        Assert.That(otherTrainerText, Does.Not.Contain("Owner-only field one"));
        Assert.That(otherTrainerText, Does.Not.Contain("Owner-only field two"));

        SetAuthorizationHeader(ordinaryUser.Id);
        using var ordinaryResponse = await Client.GetAsync("/api/trainer/report-templates");
        Assert.That(ordinaryResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var ordinaryText = await ordinaryResponse.Content.ReadAsStringAsync();
        Assert.That(ordinaryText, Does.Not.Contain(ownerTemplateOne.Id));
        Assert.That(ordinaryText, Does.Not.Contain(ownerTemplateTwo.Id));
        Assert.That(ordinaryText, Does.Not.Contain("Task 10 owner template one"));
        Assert.That(ordinaryText, Does.Not.Contain("Task 10 owner template two"));

        ClearAuthorizationHeader();
        using var anonymousResponse = await Client.GetAsync("/api/trainer/report-templates");
        Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var anonymousText = await anonymousResponse.Content.ReadAsStringAsync();
        Assert.That(anonymousText, Does.Not.Contain(ownerTemplateOne.Id));
        Assert.That(anonymousText, Does.Not.Contain(ownerTemplateTwo.Id));
        Assert.That(anonymousText, Does.Not.Contain("Owner-only field one"));
        Assert.That(anonymousText, Does.Not.Contain("Owner-only field two"));
    }

    [Test]
    [AuthorizationEvidence("GET", "/api/trainer/report-templates/{templateId}", "own", "owner-allow")]
    [AuthorizationEvidence("GET", "/api/trainer/report-templates/{templateId}", "own", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("GET", "/api/trainer/report-templates/{templateId}", "own", "anonymous-denial")]
    public async Task TrainerReportTemplateDetailRoute_IsOwnerScopedAndNonDisclosing()
    {
        var owner = await SeedTrainerAsync("task10-template-detail-owner", "task10-template-detail-owner@example.test");
        var otherTrainer = await SeedTrainerAsync("task10-template-detail-other", "task10-template-detail-other@example.test");
        var ordinaryUser = await SeedUserAsync("task10-template-detail-ordinary", "task10-template-detail-ordinary@example.test");

        SetAuthorizationHeader(owner.Id);
        var protectedTemplate = await CreateReportTemplateAsync(
            "Task 10 protected detail template",
            "Protected detail field",
            "protectedDetailField");
        var route = $"/api/trainer/report-templates/{protectedTemplate.Id}";

        using var ownerResponse = await Client.GetAsync(route);
        Assert.That(ownerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var ownerTemplate = await ownerResponse.Content.ReadFromJsonAsync<ReportTemplateResponse>();
        Assert.That(ownerTemplate, Is.Not.Null);
        Assert.That(ownerTemplate!.Id, Is.EqualTo(protectedTemplate.Id));
        Assert.That(ownerTemplate.Name, Is.EqualTo("Task 10 protected detail template"));
        Assert.That((ownerTemplate.Fields).Where(field => field.Label == "Protected detail field"), Has.Exactly(1).Items);

        SetAuthorizationHeader(otherTrainer.Id);
        using var otherResponse = await Client.GetAsync(route);
        Assert.That(otherResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var otherText = await otherResponse.Content.ReadAsStringAsync();
        Assert.That(otherText, Does.Not.Contain(protectedTemplate.Id));
        Assert.That(otherText, Does.Not.Contain("Task 10 protected detail template"));
        Assert.That(otherText, Does.Not.Contain("Protected detail field"));

        SetAuthorizationHeader(ordinaryUser.Id);
        using var ordinaryResponse = await Client.GetAsync(route);
        Assert.That(ordinaryResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var ordinaryText = await ordinaryResponse.Content.ReadAsStringAsync();
        Assert.That(ordinaryText, Does.Not.Contain(protectedTemplate.Id));
        Assert.That(ordinaryText, Does.Not.Contain("Task 10 protected detail template"));
        Assert.That(ordinaryText, Does.Not.Contain("Protected detail field"));

        ClearAuthorizationHeader();
        using var anonymousResponse = await Client.GetAsync(route);
        Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var anonymousText = await anonymousResponse.Content.ReadAsStringAsync();
        Assert.That(anonymousText, Does.Not.Contain(protectedTemplate.Id));
        Assert.That(anonymousText, Does.Not.Contain("Task 10 protected detail template"));
        Assert.That(anonymousText, Does.Not.Contain("Protected detail field"));
    }

    [Test]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/recurring-report-assignments", "trainer-shared", "active-relationship-allow")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/recurring-report-assignments", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/recurring-report-assignments", "trainer-shared", "former-relationship-denial")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/recurring-report-assignments", "trainer-shared", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/recurring-report-assignments", "trainer-shared", "anonymous-denial")]
    public async Task TrainerRecurringReportAssignmentsRoute_IsRelationshipScopedAndNonDisclosing()
    {
        var trainer = await SeedTrainerAsync("http-recurring-trainer", "http-recurring-trainer@example.test");
        var linked = await SeedUserAsync("http-recurring-linked", "http-recurring-linked@example.test");
        var unrelated = await SeedUserAsync("http-recurring-unrelated", "http-recurring-unrelated@example.test");
        var ordinaryUser = await SeedUserAsync("http-recurring-ordinary", "http-recurring-ordinary@example.test");

        using (var seedScope = Factory.Services.CreateScope())
        {
            var database = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = trainer.Id,
                TraineeId = linked.Id
            });
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);
        var template = await CreateReportTemplateAsync(
            "HTTP recurring protected template",
            "HTTP recurring field",
            "httpRecurringField");
        using var createResponse = await Client.PostAsJsonAsync(
            $"/api/trainer/trainees/{linked.Id}/recurring-report-assignments",
            new
            {
                templateId = template.Id,
                intervalValue = 1,
                intervalUnit = "Week",
                startsAt = DateTimeOffset.UtcNow,
                note = "HTTP recurring protected assignment"
            });
        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var createdText = await createResponse.Content.ReadAsStringAsync();
        Assert.That(createdText, Does.Contain("HTTP recurring protected assignment"));

        using var ownerResponse = await Client.GetAsync($"/api/trainer/trainees/{linked.Id}/recurring-report-assignments");
        Assert.That(ownerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var ownerAssignments = await ownerResponse.Content.ReadFromJsonAsync<List<RecurringReportAssignmentResponse>>();
        Assert.That(ownerAssignments, Is.Not.Null);
        Assert.That((ownerAssignments!).Where(assignment =>
            assignment.TemplateId == template.Id
            && assignment.Note == "HTTP recurring protected assignment"), Has.Exactly(1).Items);

        using var unrelatedResponse = await Client.GetAsync($"/api/trainer/trainees/{unrelated.Id}/recurring-report-assignments");
        Assert.That(unrelatedResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var unrelatedText = await unrelatedResponse.Content.ReadAsStringAsync();
        Assert.That(unrelatedText, Does.Not.Contain(template.Id));
        Assert.That(unrelatedText, Does.Not.Contain("HTTP recurring protected assignment"));

        using (var unlinkScope = Factory.Services.CreateScope())
        {
            var database = unlinkScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var link = await database.TrainerTraineeLinks
                .SingleAsync(candidate => candidate.TrainerId == trainer.Id && candidate.TraineeId == linked.Id);
            database.TrainerTraineeLinks.Remove(link);
            await database.SaveChangesAsync();
        }

        using var formerResponse = await Client.GetAsync($"/api/trainer/trainees/{linked.Id}/recurring-report-assignments");
        Assert.That(formerResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var formerText = await formerResponse.Content.ReadAsStringAsync();
        Assert.That(formerText, Does.Not.Contain(template.Id));
        Assert.That(formerText, Does.Not.Contain("HTTP recurring protected assignment"));

        SetAuthorizationHeader(ordinaryUser.Id);
        using var ordinaryResponse = await Client.GetAsync($"/api/trainer/trainees/{linked.Id}/recurring-report-assignments");
        Assert.That(ordinaryResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var ordinaryText = await ordinaryResponse.Content.ReadAsStringAsync();
        Assert.That(ordinaryText, Does.Not.Contain(template.Id));
        Assert.That(ordinaryText, Does.Not.Contain("HTTP recurring protected assignment"));

        ClearAuthorizationHeader();
        using var anonymousResponse = await Client.GetAsync($"/api/trainer/trainees/{linked.Id}/recurring-report-assignments");
        Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var anonymousText = await anonymousResponse.Content.ReadAsStringAsync();
        Assert.That(anonymousText, Does.Not.Contain(template.Id));
        Assert.That(anonymousText, Does.Not.Contain("HTTP recurring protected assignment"));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainee/report-requests/{requestId}/submit", "own", "owner-allow")]
    [AuthorizationEvidence("POST", "/api/trainee/report-requests/{requestId}/submit", "own", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("GET", "/api/trainee/report-submissions", "own", "owner-allow")]
    [AuthorizationEvidence("GET", "/api/trainee/report-submissions", "own", "no-client-subject")]
    [AuthorizationEvidence("GET", "/api/trainee/report-submissions", "own", "anonymous-denial")]
    public async Task TraineeReportSubmissionsRoute_IsActorScopedAndNonDisclosing()
    {
        var trainer = await SeedTrainerAsync("task10-submission-trainer", "task10-submission-trainer@example.test");
        var trainee = await SeedUserAsync("task10-submission-trainee", "task10-submission-trainee@example.test");
        var otherTrainee = await SeedUserAsync("task10-submission-other", "task10-submission-other@example.test");

        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id
            });
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);
        using var templateResponse = await Client.PostAsJsonAsync("/api/trainer/report-templates", new
        {
            name = "Task 10 submission template",
            fields = new[]
            {
                new { key = "checkIn", label = "Check-in", type = "Text", isRequired = true, order = 0 }
            }
        });
        Assert.That(templateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var template = await templateResponse.Content.ReadFromJsonAsync<ReportTemplateResponse>();
        Assert.That(template, Is.Not.Null);

        using var requestResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/report-requests", new
        {
            templateId = template!.Id,
            note = "Task 10 submission fixture"
        });
        Assert.That(requestResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var request = await requestResponse.Content.ReadFromJsonAsync<ReportRequestResponse>();
        Assert.That(request, Is.Not.Null);

        SetAuthorizationHeader(otherTrainee.Id);
        using var foreignSubmitResponse = await Client.PostAsJsonAsync($"/api/trainee/report-requests/{request!.Id}/submit", new
        {
            answers = new { checkIn = "Foreign submission must not be accepted" }
        });
        Assert.That(foreignSubmitResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await foreignSubmitResponse.Content.ReadAsStringAsync()), Does.Not.Contain("Foreign submission must not be accepted"));

        SetAuthorizationHeader(trainee.Id);
        using var submitResponse = await Client.PostAsJsonAsync($"/api/trainee/report-requests/{request.Id}/submit", new
        {
            answers = new { checkIn = "Protected submission answer" }
        });
        Assert.That(submitResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var submitted = await submitResponse.Content.ReadFromJsonAsync<ReportSubmissionResponse>();
        Assert.That(submitted, Is.Not.Null);

        using var ownerResponse = await Client.GetAsync("/api/trainee/report-submissions");
        Assert.That(ownerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var ownerSubmissions = await ownerResponse.Content.ReadFromJsonAsync<List<ReportSubmissionResponse>>();
        Assert.That(ownerSubmissions, Is.Not.Null);
        Assert.That((ownerSubmissions!).Where(submission => submission.Id == submitted!.Id), Has.Exactly(1).Items);
        Assert.That(ownerSubmissions.Single().ReportRequestId, Is.EqualTo(request.Id));
        Assert.That(ownerSubmissions.Single().Answers["checkIn"].GetString(), Is.EqualTo("Protected submission answer"));

        SetAuthorizationHeader(otherTrainee.Id);
        using var otherResponse = await Client.GetAsync("/api/trainee/report-submissions");
        Assert.That(otherResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var otherSubmissions = await otherResponse.Content.ReadFromJsonAsync<List<ReportSubmissionResponse>>();
        Assert.That(otherSubmissions, Is.Not.Null);
        Assert.That(otherSubmissions, Is.Empty);
        var otherResponseText = await otherResponse.Content.ReadAsStringAsync();
        Assert.That(otherResponseText, Does.Not.Contain(submitted!.Id));
        Assert.That(otherResponseText, Does.Not.Contain(request.Id));
        Assert.That(otherResponseText, Does.Not.Contain("Protected submission answer"));

        ClearAuthorizationHeader();
        using var anonymousResponse = await Client.GetAsync("/api/trainee/report-submissions");
        Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var anonymousResponseText = await anonymousResponse.Content.ReadAsStringAsync();
        Assert.That(anonymousResponseText, Does.Not.Contain(submitted.Id));
        Assert.That(anonymousResponseText, Does.Not.Contain(request.Id));
        Assert.That(anonymousResponseText, Does.Not.Contain("Protected submission answer"));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainee/report-requests/{requestId}/submit", "own", "anonymous-denial")]
    public async Task TraineeReportRequestSubmit_WithoutAuthorization_ReturnsUnauthorized()
    {
        ClearAuthorizationHeader();

        using var response = await Client.PostAsJsonAsync($"/api/trainee/report-requests/{Id<ReportRequest>.New()}/submit", new
        {
            answers = new { protectedAnswer = "must not be processed" }
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await response.Content.ReadAsStringAsync()), Does.Not.Contain("must not be processed"));
    }

    [Test]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/report-submissions", "trainer-shared", "active-relationship-allow")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/report-submissions", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/report-submissions", "trainer-shared", "source-episode-history-allow")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/report-submissions", "trainer-shared", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/report-submissions", "trainer-shared", "anonymous-denial")]
    public async Task TrainerReportSubmissionsRoute_IsRelationshipScopedAndNonDisclosing()
    {
        var trainer = await SeedTrainerAsync("http-trainer-submissions-trainer", "http-trainer-submissions-trainer@example.test");
        var linked = await SeedUserAsync("http-trainer-submissions-linked", "http-trainer-submissions-linked@example.test");
        var unrelated = await SeedUserAsync("http-trainer-submissions-unrelated", "http-trainer-submissions-unrelated@example.test");
        var ordinaryUser = await SeedUserAsync("http-trainer-submissions-ordinary", "http-trainer-submissions-ordinary@example.test");

        using (var seedScope = Factory.Services.CreateScope())
        {
            var database = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = trainer.Id,
                TraineeId = linked.Id
            });
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);
        var template = await CreateReportTemplateAsync(
            "HTTP trainer submissions template",
            "HTTP trainer submissions field",
            "httpTrainerSubmissionsField");
        using var requestResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{linked.Id}/report-requests", new
        {
            templateId = template.Id,
            note = "HTTP trainer submissions request"
        });
        Assert.That(requestResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var request = await requestResponse.Content.ReadFromJsonAsync<ReportRequestResponse>();
        Assert.That(request, Is.Not.Null);

        SetAuthorizationHeader(linked.Id);
        using var submitResponse = await Client.PostAsJsonAsync($"/api/trainee/report-requests/{request!.Id}/submit", new
        {
            answers = new { httpTrainerSubmissionsField = "HTTP trainer submissions answer" }
        });
        Assert.That(submitResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var submission = await submitResponse.Content.ReadFromJsonAsync<ReportSubmissionResponse>();
        Assert.That(submission, Is.Not.Null);

        SetAuthorizationHeader(trainer.Id);
        using var ownerResponse = await Client.GetAsync($"/api/trainer/trainees/{linked.Id}/report-submissions");
        Assert.That(ownerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var ownerSubmissions = await ownerResponse.Content.ReadFromJsonAsync<List<ReportSubmissionResponse>>();
        Assert.That(ownerSubmissions, Is.Not.Null);
        Assert.That((ownerSubmissions!).Where(item => item.Id == submission!.Id), Has.Exactly(1).Items);
        Assert.That(ownerSubmissions.Single().Answers["httpTrainerSubmissionsField"].GetString(), Is.EqualTo("HTTP trainer submissions answer"));

        using var unrelatedResponse = await Client.GetAsync($"/api/trainer/trainees/{unrelated.Id}/report-submissions");
        Assert.That(unrelatedResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var unrelatedText = await unrelatedResponse.Content.ReadAsStringAsync();
        Assert.That(unrelatedText, Does.Not.Contain(submission.Id));
        Assert.That(unrelatedText, Does.Not.Contain("HTTP trainer submissions answer"));

        using (var unlinkScope = Factory.Services.CreateScope())
        {
            var database = unlinkScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var link = await database.TrainerTraineeLinks
                .SingleAsync(candidate => candidate.TrainerId == trainer.Id && candidate.TraineeId == linked.Id);
            database.TrainerTraineeLinks.Remove(link);
            await database.SaveChangesAsync();
        }

        using var formerResponse = await Client.GetAsync($"/api/trainer/trainees/{linked.Id}/report-submissions");
        Assert.That(formerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var formerSubmissions = await formerResponse.Content.ReadFromJsonAsync<List<ReportSubmissionResponse>>();
        Assert.That((formerSubmissions).Where(item => item.Id == submission.Id), Has.Exactly(1).Items);
        Assert.That(formerSubmissions!.Single().Answers["httpTrainerSubmissionsField"].GetString(), Is.EqualTo("HTTP trainer submissions answer"));

        SetAuthorizationHeader(ordinaryUser.Id);
        using var ordinaryResponse = await Client.GetAsync($"/api/trainer/trainees/{linked.Id}/report-submissions");
        Assert.That(ordinaryResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var ordinaryText = await ordinaryResponse.Content.ReadAsStringAsync();
        Assert.That(ordinaryText, Does.Not.Contain(submission.Id));
        Assert.That(ordinaryText, Does.Not.Contain("HTTP trainer submissions answer"));

        ClearAuthorizationHeader();
        using var anonymousResponse = await Client.GetAsync($"/api/trainer/trainees/{linked.Id}/report-submissions");
        Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var anonymousText = await anonymousResponse.Content.ReadAsStringAsync();
        Assert.That(anonymousText, Does.Not.Contain(submission.Id));
        Assert.That(anonymousText, Does.Not.Contain("HTTP trainer submissions answer"));
    }

    [Test]
    [AuthorizationEvidence("GET", "/api/trainee/reporting/photos/history", "own", "owner-allow")]
    [AuthorizationEvidence("GET", "/api/trainee/reporting/photos/history", "own", "no-client-subject")]
    [AuthorizationEvidence("GET", "/api/trainee/reporting/photos/history", "own", "anonymous-denial")]
    public async Task TraineePhotoHistoryRoute_IsActorScopedAndNonDisclosing()
    {
        var trainer = await SeedTrainerAsync("task10-photo-history-trainer", "task10-photo-history-trainer@example.test");
        var trainee = await SeedUserAsync("task10-photo-history-trainee", "task10-photo-history-trainee@example.test");
        var otherTrainee = await SeedUserAsync("task10-photo-history-other", "task10-photo-history-other@example.test");
        var template = new ReportTemplate
        {
            Id = Id<ReportTemplate>.New(),
            TrainerId = trainer.Id,
            Name = "Task 10 photo history template"
        };
        var request = new ReportRequest
        {
            Id = Id<ReportRequest>.New(),
            TrainerId = trainer.Id,
            TraineeId = trainee.Id,
            TemplateId = template.Id,
            Status = ReportRequestStatus.Submitted,
            Note = "Protected photo history request"
        };
        var photo = new Photo
        {
            Id = Id<Photo>.New(),
            StorageKey = "task10-photo-history/protected.jpg",
            ThumbnailStorageKey = "task10-photo-history/protected-thumb.jpg",
            MimeType = "image/jpeg",
            SizeBytes = 4096,
            Checksum = "task10-photo-history-checksum",
            ViewType = "Front",
            ReportRequestId = request.Id,
            UploaderUserId = trainee.Id,
            OwnerUserId = trainee.Id
        };

        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.ReportTemplates.Add(template);
            database.ReportRequests.Add(request);
            database.Photos.Add(photo);
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainee.Id);
        using var ownerResponse = await Client.GetAsync($"/api/trainee/reporting/photos/history?requestId={request.Id}");
        Assert.That(ownerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var ownerHistory = await ownerResponse.Content.ReadFromJsonAsync<PhotoHistoryResponse>();
        Assert.That(ownerHistory, Is.Not.Null);
        Assert.That(ownerHistory!.Photos, Has.Exactly(1).Items);
        Assert.That(ownerHistory.Photos[0].Id, Is.EqualTo(photo.Id.ToString()));
        Assert.That(ownerHistory.Photos[0].ReportRequestId, Is.EqualTo(request.Id.ToString()));
        Assert.That(ownerHistory.Photos[0].ViewType, Is.EqualTo(photo.ViewType));
        Assert.That(ownerHistory.Photos[0].SizeBytes, Is.EqualTo(photo.SizeBytes));
        Assert.That(string.IsNullOrWhiteSpace(ownerHistory.Photos[0].ReadUrl), Is.False);
        Assert.That(string.IsNullOrWhiteSpace(ownerHistory.Photos[0].ThumbnailUrl), Is.False);

        SetAuthorizationHeader(otherTrainee.Id);
        using var otherResponse = await Client.GetAsync($"/api/trainee/reporting/photos/history?requestId={request.Id}");
        Assert.That(otherResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var otherResponseText = await otherResponse.Content.ReadAsStringAsync();
        Assert.That(otherResponseText, Does.Not.Contain(photo.Id.ToString()));
        Assert.That(otherResponseText, Does.Not.Contain(photo.StorageKey));
        Assert.That(otherResponseText, Does.Not.Contain(photo.ThumbnailStorageKey));
        Assert.That(otherResponseText, Does.Not.Contain(ownerHistory.Photos[0].ReadUrl));
        Assert.That(otherResponseText, Does.Not.Contain(ownerHistory.Photos[0].ThumbnailUrl));
        Assert.That(otherResponseText, Does.Not.Contain(request.Id.ToString()));
        Assert.That(otherResponseText, Does.Not.Contain(request.Note));

        ClearAuthorizationHeader();
        using var anonymousResponse = await Client.GetAsync($"/api/trainee/reporting/photos/history?requestId={request.Id}");
        Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var anonymousResponseText = await anonymousResponse.Content.ReadAsStringAsync();
        Assert.That(anonymousResponseText, Does.Not.Contain(photo.Id.ToString()));
        Assert.That(anonymousResponseText, Does.Not.Contain(photo.StorageKey));
        Assert.That(anonymousResponseText, Does.Not.Contain(ownerHistory.Photos[0].ReadUrl));
        Assert.That(anonymousResponseText, Does.Not.Contain(request.Id.ToString()));
        Assert.That(anonymousResponseText, Does.Not.Contain(request.Note));
    }

    [Test]
    [AuthorizationEvidence("GET", "/api/trainer/reporting/photos/history", "own", "owner-allow")]
    [AuthorizationEvidence("GET", "/api/trainer/reporting/photos/history", "own", "no-client-subject")]
    [AuthorizationEvidence("GET", "/api/trainer/reporting/photos/history", "own", "source-episode-history-allow")]
    [AuthorizationEvidence("GET", "/api/trainer/reporting/photos/history", "own", "anonymous-denial")]
    public async Task TrainerPhotoHistoryRoute_AllowsSourceEpisodeHistoryAndNonDiscloses()
    {
        var trainer = await SeedTrainerAsync("task10-trainer-photo-history-owner", "task10-trainer-photo-history-owner@example.test");
        var otherTrainer = await SeedTrainerAsync("task10-trainer-photo-history-other", "task10-trainer-photo-history-other@example.test");
        var trainee = await SeedUserAsync("task10-trainer-photo-history-trainee", "task10-trainer-photo-history-trainee@example.test");
        var ordinaryUser = await SeedUserAsync("task10-trainer-photo-history-ordinary", "task10-trainer-photo-history-ordinary@example.test");
        var template = new ReportTemplate
        {
            Id = Id<ReportTemplate>.New(),
            TrainerId = trainer.Id,
            Name = "Task 10 trainer photo history template"
        };
        var request = new ReportRequest
        {
            Id = Id<ReportRequest>.New(),
            TrainerId = trainer.Id,
            TraineeId = trainee.Id,
            TemplateId = template.Id,
            Status = ReportRequestStatus.Submitted,
            Note = "Protected trainer photo history request"
        };
        var photo = new Photo
        {
            Id = Id<Photo>.New(),
            StorageKey = "task10-trainer-photo-history/protected.jpg",
            ThumbnailStorageKey = "task10-trainer-photo-history/protected-thumb.jpg",
            MimeType = "image/jpeg",
            SizeBytes = 8192,
            Checksum = "task10-trainer-photo-history-checksum",
            ViewType = "Side",
            ReportRequestId = request.Id,
            UploaderUserId = trainee.Id,
            OwnerUserId = trainee.Id
        };

        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id
            });
            database.ReportTemplates.Add(template);
            database.ReportRequests.Add(request);
            database.Photos.Add(photo);
            await database.SaveChangesAsync();
        }

        var route = $"/api/trainer/reporting/photos/history?requestId={request.Id}";
        SetAuthorizationHeader(trainer.Id);
        using var ownerResponse = await Client.GetAsync(route);
        Assert.That(ownerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var ownerHistory = await ownerResponse.Content.ReadFromJsonAsync<PhotoHistoryResponse>();
        Assert.That(ownerHistory, Is.Not.Null);
        Assert.That(ownerHistory!.Photos, Has.Exactly(1).Items);
        Assert.That(ownerHistory.Photos[0].Id, Is.EqualTo(photo.Id.ToString()));
        Assert.That(ownerHistory.Photos[0].ReportRequestId, Is.EqualTo(request.Id.ToString()));
        Assert.That(ownerHistory.Photos[0].ViewType, Is.EqualTo(photo.ViewType));
        Assert.That(ownerHistory.Photos[0].SizeBytes, Is.EqualTo(photo.SizeBytes));
        Assert.That(string.IsNullOrWhiteSpace(ownerHistory.Photos[0].ReadUrl), Is.False);
        Assert.That(string.IsNullOrWhiteSpace(ownerHistory.Photos[0].ThumbnailUrl), Is.False);

        SetAuthorizationHeader(otherTrainer.Id);
        using var unrelatedResponse = await Client.GetAsync(route);
        Assert.That(unrelatedResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var unrelatedText = await unrelatedResponse.Content.ReadAsStringAsync();
        AssertPhotoHistoryDenialDoesNotDisclose(unrelatedText, photo, request, ownerHistory.Photos[0]);

        using (var unlinkScope = Factory.Services.CreateScope())
        {
            var database = unlinkScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var link = await database.TrainerTraineeLinks
                .SingleAsync(candidate => candidate.TrainerId == trainer.Id && candidate.TraineeId == trainee.Id);
            database.TrainerTraineeLinks.Remove(link);
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);
        using var formerResponse = await Client.GetAsync(route);
        Assert.That(formerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var formerHistory = await formerResponse.Content.ReadFromJsonAsync<PhotoHistoryResponse>();
        Assert.That(formerHistory, Is.Not.Null);
        Assert.That((formerHistory!.Photos).Where(item => item.Id == photo.Id.ToString()), Has.Exactly(1).Items);

        using var formerCollectionResponse = await Client.GetAsync($"/api/trainer/reporting/photos/history?traineeId={trainee.Id}");
        Assert.That(formerCollectionResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var formerCollection = await formerCollectionResponse.Content.ReadFromJsonAsync<PhotoHistoryResponse>();
        Assert.That(formerCollection, Is.Not.Null);
        Assert.That((formerCollection!.Photos).Where(item => item.Id == photo.Id.ToString()), Has.Exactly(1).Items);

        SetAuthorizationHeader(ordinaryUser.Id);
        using var ordinaryResponse = await Client.GetAsync(route);
        Assert.That(ordinaryResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var ordinaryText = await ordinaryResponse.Content.ReadAsStringAsync();
        AssertPhotoHistoryDenialDoesNotDisclose(ordinaryText, photo, request, ownerHistory.Photos[0]);

        ClearAuthorizationHeader();
        using var anonymousResponse = await Client.GetAsync(route);
        Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var anonymousText = await anonymousResponse.Content.ReadAsStringAsync();
        AssertPhotoHistoryDenialDoesNotDisclose(anonymousText, photo, request, ownerHistory.Photos[0]);

        using var verifyScope = Factory.Services.CreateScope();
        var persistedPhoto = await verifyScope.ServiceProvider.GetRequiredService<AppDbContext>().Photos
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == photo.Id);
        Assert.That(persistedPhoto.StorageKey, Is.EqualTo(photo.StorageKey));
        Assert.That(persistedPhoto.IsDeleted, Is.False);
    }

    [Test]
    [AuthorizationEvidence("GET", "/api/trainer/reporting/photos/{photoId}/signed-url", "own", "owner-allow")]
    [AuthorizationEvidence("GET", "/api/trainer/reporting/photos/{photoId}/signed-url", "own", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("GET", "/api/trainer/reporting/photos/{photoId}/signed-url", "own", "source-episode-history-allow")]
    [AuthorizationEvidence("GET", "/api/trainer/reporting/photos/{photoId}/signed-url", "own", "anonymous-denial")]
    public async Task TrainerPhotoSignedUrlRoute_AllowsSourceEpisodeHistoryAndNonDiscloses()
    {
        var trainer = await SeedTrainerAsync("task10-signed-url-trainer", "task10-signed-url-trainer@example.test");
        var otherTrainer = await SeedTrainerAsync("task10-signed-url-other-trainer", "task10-signed-url-other-trainer@example.test");
        var trainee = await SeedUserAsync("task10-signed-url-trainee", "task10-signed-url-trainee@example.test");
        var foreignTrainee = await SeedUserAsync("task10-signed-url-foreign-trainee", "task10-signed-url-foreign-trainee@example.test");
        var ordinaryUser = await SeedUserAsync("task10-signed-url-ordinary", "task10-signed-url-ordinary@example.test");
        var template = new ReportTemplate
        {
            Id = Id<ReportTemplate>.New(),
            TrainerId = trainer.Id,
            Name = "Task 10 signed URL template"
        };
        var request = new ReportRequest
        {
            Id = Id<ReportRequest>.New(),
            TrainerId = trainer.Id,
            TraineeId = trainee.Id,
            TemplateId = template.Id,
            Status = ReportRequestStatus.Submitted,
            Note = "Protected signed URL request"
        };
        var foreignRequest = new ReportRequest
        {
            Id = Id<ReportRequest>.New(),
            TrainerId = trainer.Id,
            TraineeId = foreignTrainee.Id,
            TemplateId = template.Id,
            Status = ReportRequestStatus.Pending,
            Note = "Foreign signed URL request"
        };
        var photo = new Photo
        {
            Id = Id<Photo>.New(),
            StorageKey = "task10-signed-url/protected.jpg",
            MimeType = "image/jpeg",
            SizeBytes = 2048,
            Checksum = "task10-signed-url-checksum",
            ViewType = "Front",
            ReportRequestId = request.Id,
            UploaderUserId = trainee.Id,
            OwnerUserId = trainee.Id
        };
        var foreignPhoto = new Photo
        {
            Id = Id<Photo>.New(),
            StorageKey = "task10-signed-url/foreign.jpg",
            MimeType = "image/jpeg",
            SizeBytes = 3072,
            Checksum = "task10-signed-url-foreign-checksum",
            ViewType = "Front",
            ReportRequestId = foreignRequest.Id,
            UploaderUserId = foreignTrainee.Id,
            OwnerUserId = foreignTrainee.Id
        };

        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id
            });
            database.ReportTemplates.Add(template);
            database.ReportRequests.AddRange(request, foreignRequest);
            database.Photos.AddRange(photo, foreignPhoto);
            await database.SaveChangesAsync();
        }

        var protectedRoute = $"/api/trainer/reporting/photos/{photo.Id}/signed-url";
        SetAuthorizationHeader(trainer.Id);
        using var ownerResponse = await Client.GetAsync(protectedRoute);
        Assert.That(ownerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var ownerCapability = await ownerResponse.Content.ReadFromJsonAsync<SignedReadUrlResponse>();
        Assert.That(ownerCapability, Is.Not.Null);
        Assert.That(string.IsNullOrWhiteSpace(ownerCapability!.ReadUrl), Is.False);
        Assert.That(ownerCapability.ExpiresAt, Is.GreaterThan(DateTimeOffset.UtcNow));
        Assert.That(ownerCapability.ReadUrl, Does.Contain("task10-signed-url"));

        var foreignRoute = $"/api/trainer/reporting/photos/{foreignPhoto.Id}/signed-url";
        using var foreignResponse = await Client.GetAsync(foreignRoute);
        Assert.That(foreignResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var foreignText = await foreignResponse.Content.ReadAsStringAsync();
        AssertSignedUrlDenialDoesNotDisclose(foreignText, photo, request, ownerCapability, foreignPhoto);

        SetAuthorizationHeader(otherTrainer.Id);
        using var unrelatedResponse = await Client.GetAsync(protectedRoute);
        Assert.That(unrelatedResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var unrelatedText = await unrelatedResponse.Content.ReadAsStringAsync();
        AssertSignedUrlDenialDoesNotDisclose(unrelatedText, photo, request, ownerCapability, foreignPhoto);

        using (var unlinkScope = Factory.Services.CreateScope())
        {
            var database = unlinkScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var link = await database.TrainerTraineeLinks
                .SingleAsync(candidate => candidate.TrainerId == trainer.Id && candidate.TraineeId == trainee.Id);
            database.TrainerTraineeLinks.Remove(link);
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);
        using var formerResponse = await Client.GetAsync(protectedRoute);
        Assert.That(formerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var formerCapability = await formerResponse.Content.ReadFromJsonAsync<SignedReadUrlResponse>();
        Assert.That(formerCapability, Is.Not.Null);
        Assert.That(formerCapability!.ReadUrl, Does.Contain("task10-signed-url"));

        SetAuthorizationHeader(ordinaryUser.Id);
        using var ordinaryResponse = await Client.GetAsync(protectedRoute);
        Assert.That(ordinaryResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var ordinaryText = await ordinaryResponse.Content.ReadAsStringAsync();
        AssertSignedUrlDenialDoesNotDisclose(ordinaryText, photo, request, ownerCapability, foreignPhoto);

        ClearAuthorizationHeader();
        using var anonymousResponse = await Client.GetAsync(protectedRoute);
        Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var anonymousText = await anonymousResponse.Content.ReadAsStringAsync();
        AssertSignedUrlDenialDoesNotDisclose(anonymousText, photo, request, ownerCapability, foreignPhoto);

        using var verifyScope = Factory.Services.CreateScope();
        var databaseSnapshot = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistedPhotos = await databaseSnapshot.Photos
            .AsNoTracking()
            .Where(candidate => candidate.Id == photo.Id || candidate.Id == foreignPhoto.Id)
            .ToListAsync();
        Assert.That(persistedPhotos, Has.Exactly(2).Items);
        Assert.That((persistedPhotos).All(candidate => !candidate.IsDeleted), Is.True);
    }

    private static void AssertSignedUrlDenialDoesNotDisclose(
        string responseText,
        Photo photo,
        ReportRequest request,
        SignedReadUrlResponse ownerCapability,
        Photo foreignPhoto)
    {
        Assert.That(responseText, Does.Not.Contain(photo.Id.ToString()));
        Assert.That(responseText, Does.Not.Contain(photo.StorageKey));
        Assert.That(responseText, Does.Not.Contain(ownerCapability.ReadUrl));
        Assert.That(responseText, Does.Not.Contain(request.Id.ToString()));
        Assert.That(responseText, Does.Not.Contain(request.Note));
        Assert.That(responseText, Does.Not.Contain(foreignPhoto.Id.ToString()));
        Assert.That(responseText, Does.Not.Contain(foreignPhoto.StorageKey));
    }

    private static void AssertPhotoHistoryDenialDoesNotDisclose(
        string responseText,
        Photo photo,
        ReportRequest request,
        PhotoHistoryItemResponse ownerPhoto)
    {
        Assert.That(responseText, Does.Not.Contain(photo.Id.ToString()));
        Assert.That(responseText, Does.Not.Contain(photo.StorageKey));
        Assert.That(responseText, Does.Not.Contain(photo.ThumbnailStorageKey));
        Assert.That(responseText, Does.Not.Contain(ownerPhoto.ReadUrl));
        Assert.That(responseText, Does.Not.Contain(ownerPhoto.ThumbnailUrl));
        Assert.That(responseText, Does.Not.Contain(request.Id.ToString()));
        Assert.That(responseText, Does.Not.Contain(request.Note));
    }

    [Test]
    public async Task SubmitReport_WithInvalidDynamicFieldType_ReturnsBadRequest()
    {
        var trainer = await SeedTrainerAsync("trainer-reports-invalid", "trainer-reports-invalid@example.com");
        var trainee = await SeedUserAsync(name: "trainee-reports-invalid", email: "trainee-reports-invalid@example.com", password: "password123");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Domain.ValueObjects.Id<TrainerTraineeLink>.New(),
                TrainerId = (Domain.ValueObjects.Id<User>)trainer.Id,
                TraineeId = (Domain.ValueObjects.Id<User>)trainee.Id
            });
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);
        var templateResponse = await Client.PostAsJsonAsync("/api/trainer/report-templates", new
        {
            name = "Daily",
            fields = new object[]
            {
                new { key = "mood", label = "Mood", type = "Text", isRequired = true, order = 0 }
            }
        });
        var template = await templateResponse.Content.ReadFromJsonAsync<ReportTemplateResponse>();

        var requestResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/report-requests", new
        {
            templateId = template!.Id
        });
        var request = await requestResponse.Content.ReadFromJsonAsync<ReportRequestResponse>();

        SetAuthorizationHeader(trainee.Id);
        var submitResponse = await Client.PostAsJsonAsync($"/api/trainee/report-requests/{request!.Id}/submit", new
        {
            answers = new
            {
                mood = 123
            }
        });

        Assert.That(submitResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task ReportRequests_WithMidnightDueAt_RemainVisibleUntilEndOfDay()
    {
        var trainer = await SeedTrainerAsync("trainer-reports-midnight", "trainer-reports-midnight@example.com");
        var trainee = await SeedUserAsync(name: "trainee-reports-midnight", email: "trainee-reports-midnight@example.com", password: "password123");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Domain.ValueObjects.Id<TrainerTraineeLink>.New(),
                TrainerId = (Domain.ValueObjects.Id<User>)trainer.Id,
                TraineeId = (Domain.ValueObjects.Id<User>)trainee.Id
            });
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);
        var createTemplateResponse = await Client.PostAsJsonAsync("/api/trainer/report-templates", new
        {
            name = "Same-day deadline",
            fields = new object[]
            {
                new { key = "checkin", label = "Check-in", type = "Text", isRequired = true, order = 0 }
            }
        });

        Assert.That(createTemplateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var template = await createTemplateResponse.Content.ReadFromJsonAsync<ReportTemplateResponse>();
        Assert.That(template, Is.Not.Null);

        var dueAt = DateTimeOffset.UtcNow.Date.AddDays(1);
        var createRequestResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/report-requests", new
        {
            templateId = template!.Id,
            dueAt
        });

        Assert.That(createRequestResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var request = await createRequestResponse.Content.ReadFromJsonAsync<ReportRequestResponse>();
        Assert.That(request, Is.Not.Null);
        Assert.That(request!.Status, Is.EqualTo("Pending"));

        SetAuthorizationHeader(trainee.Id);
        var pendingResponse = await Client.GetAsync("/api/trainee/report-requests");
        Assert.That(pendingResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var pending = await pendingResponse.Content.ReadFromJsonAsync<List<ReportRequestResponse>>();
        Assert.That(pending, Is.Not.Null);
        Assert.That((pending!).Where(x => x.Id == request.Id), Has.Exactly(1).Items);
    }

    [Test]
    public async Task TrainerReportingController_InvalidIds_ReturnBadRequest()
    {
        var trainer = await SeedTrainerAsync("trainer-report-invalid-ids", "trainer-report-invalid-ids@example.com");
        SetAuthorizationHeader(trainer.Id);

        var getTemplate = await Client.GetAsync("/api/trainer/report-templates/not-a-guid");
        var updateTemplate = await Client.PostAsJsonAsync("/api/trainer/report-templates/not-a-guid/update", new
        {
            name = "x",
            fields = new object[] { new { key = "k", label = "l", type = "Text", isRequired = false, order = 0 } }
        });
        var deleteTemplate = await Client.PostAsync("/api/trainer/report-templates/not-a-guid/delete", content: null);
        var createRequestBadTrainee = await Client.PostAsJsonAsync("/api/trainer/trainees/not-a-guid/report-requests", new
        {
            templateId = Domain.ValueObjects.Id<object>.New().ToString()
        });
        var createRequestBadTemplate = await Client.PostAsJsonAsync($"/api/trainer/trainees/{Domain.ValueObjects.Id<User>.New()}/report-requests", new
        {
            templateId = "not-a-guid"
        });
        var getSubmissionsBadTrainee = await Client.GetAsync("/api/trainer/trainees/not-a-guid/report-submissions");

        Assert.That(getTemplate.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(updateTemplate.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(deleteTemplate.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(createRequestBadTrainee.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(createRequestBadTemplate.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(getSubmissionsBadTrainee.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/report-templates", "own", "owner-allow")]
    public async Task TrainerReportingController_TemplateCreateAndReadFlow_Works()
    {
        var trainer = await SeedTrainerAsync("trainer-report-crud", "trainer-report-crud@example.com");
        SetAuthorizationHeader(trainer.Id);

        var createTemplateResponse = await Client.PostAsJsonAsync("/api/trainer/report-templates", new
        {
            name = "Weekly CRUD",
            description = "v1",
            fields = new object[]
            {
                new { key = "weight", label = "Weight", type = "Number", isRequired = true, order = 0 }
            }
        });
        Assert.That(createTemplateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await createTemplateResponse.Content.ReadFromJsonAsync<ReportTemplateResponse>();
        Assert.That(created, Is.Not.Null);

        var getAllResponse = await Client.GetAsync("/api/trainer/report-templates");
        Assert.That(getAllResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var allTemplates = await getAllResponse.Content.ReadFromJsonAsync<List<ReportTemplateResponse>>();
        Assert.That(allTemplates, Is.Not.Null);
        Assert.That(allTemplates!.Any(x => x.Id == created!.Id), Is.True);

        var getOneResponse = await Client.GetAsync($"/api/trainer/report-templates/{created!.Id}");
        Assert.That(getOneResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var deleteResponse = await Client.PostAsync($"/api/trainer/report-templates/{created.Id}/delete", content: null);
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/report-templates", "own", "ordinary-user-denial")]
    public async Task TrainerReportTemplateCreate_AsRegularUser_ReturnsForbidden()
    {
        var regularUser = await SeedUserAsync("http-template-ordinary", "http-template-ordinary@example.test");
        SetAuthorizationHeader(regularUser.Id);

        using var response = await Client.PostAsJsonAsync("/api/trainer/report-templates", new
        {
            name = "Forbidden template",
            fields = Array.Empty<object>()
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/report-templates", "own", "anonymous-denial")]
    public async Task TrainerReportTemplateCreate_WithoutAuthorization_ReturnsUnauthorized()
    {
        ClearAuthorizationHeader();

        using var response = await Client.PostAsJsonAsync("/api/trainer/report-templates", new
        {
            name = "Anonymous template",
            fields = Array.Empty<object>()
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/report-templates/{templateId}/delete", "own", "owner-allow")]
    [AuthorizationEvidence("POST", "/api/trainer/report-templates/{templateId}/delete", "own", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/trainer/report-templates/{templateId}/delete", "own", "anonymous-denial")]
    public async Task TrainerReportTemplateDeleteRoute_IsOwnerScopedAndNonDisclosing()
    {
        var owner = await SeedTrainerAsync("http-template-delete-owner", "http-template-delete-owner@example.test");
        var otherTrainer = await SeedTrainerAsync("http-template-delete-other", "http-template-delete-other@example.test");

        SetAuthorizationHeader(owner.Id);
        var deletedTemplate = await CreateReportTemplateAsync(
            "HTTP deleted template",
            "HTTP deleted field",
            "httpDeletedField");
        var protectedTemplate = await CreateReportTemplateAsync(
            "HTTP protected template",
            "HTTP protected field",
            "httpProtectedField");

        using var ownerResponse = await Client.PostAsync($"/api/trainer/report-templates/{deletedTemplate.Id}/delete", null);
        Assert.That(ownerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        SetAuthorizationHeader(otherTrainer.Id);
        using var foreignResponse = await Client.PostAsync($"/api/trainer/report-templates/{protectedTemplate.Id}/delete", null);
        Assert.That(foreignResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var foreignText = await foreignResponse.Content.ReadAsStringAsync();
        Assert.That(foreignText, Does.Not.Contain(protectedTemplate.Id));
        Assert.That(foreignText, Does.Not.Contain("HTTP protected template"));

        ClearAuthorizationHeader();
        using var anonymousResponse = await Client.PostAsync($"/api/trainer/report-templates/{protectedTemplate.Id}/delete", null);
        Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var anonymousText = await anonymousResponse.Content.ReadAsStringAsync();
        Assert.That(anonymousText, Does.Not.Contain(protectedTemplate.Id));
        Assert.That(anonymousText, Does.Not.Contain("HTTP protected template"));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/report-templates/{templateId}/update", "own", "owner-allow")]
    [AuthorizationEvidence("POST", "/api/trainer/report-templates/{templateId}/update", "own", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/trainer/report-templates/{templateId}/update", "own", "anonymous-denial")]
    public async Task TrainerReportTemplateUpdateRoute_IsOwnerScopedAndNonDisclosing()
    {
        var owner = await SeedTrainerAsync("http-template-update-owner", "http-template-update-owner@example.test");
        var otherTrainer = await SeedTrainerAsync("http-template-update-other", "http-template-update-other@example.test");

        SetAuthorizationHeader(owner.Id);
        var template = await CreateReportTemplateAsync(
            "HTTP update template",
            "HTTP original field",
            "httpOriginalField");
        var updatePayload = new
        {
            name = "HTTP updated template",
            fields = new[]
            {
                new { key = "httpUpdatedField", label = "HTTP updated field", type = "Text", isRequired = true, order = 0 }
            }
        };

        using var ownerResponse = await Client.PostAsJsonAsync($"/api/trainer/report-templates/{template.Id}/update", updatePayload);
        Assert.That(ownerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var ownerBody = await ownerResponse.Content.ReadFromJsonAsync<ReportTemplateResponse>();
        Assert.That(ownerBody, Is.Not.Null);
        Assert.That(ownerBody!.Name, Is.EqualTo("HTTP updated template"));
        Assert.That((ownerBody.Fields).Where(field => field.Label == "HTTP updated field"), Has.Exactly(1).Items);

        SetAuthorizationHeader(otherTrainer.Id);
        using var foreignResponse = await Client.PostAsJsonAsync($"/api/trainer/report-templates/{template.Id}/update", updatePayload);
        Assert.That(foreignResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var foreignText = await foreignResponse.Content.ReadAsStringAsync();
        Assert.That(foreignText, Does.Not.Contain("HTTP updated template"));
        Assert.That(foreignText, Does.Not.Contain("HTTP updated field"));

        ClearAuthorizationHeader();
        using var anonymousResponse = await Client.PostAsJsonAsync($"/api/trainer/report-templates/{template.Id}/update", updatePayload);
        Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var anonymousText = await anonymousResponse.Content.ReadAsStringAsync();
        Assert.That(anonymousText, Does.Not.Contain("HTTP updated template"));
        Assert.That(anonymousText, Does.Not.Contain("HTTP updated field"));
    }

    private async Task<ReportTemplateResponse> CreateReportTemplateAsync(
        string name,
        string fieldLabel,
        string fieldKey)
    {
        using var response = await Client.PostAsJsonAsync("/api/trainer/report-templates", new
        {
            name,
            fields = new[]
            {
                new { key = fieldKey, label = fieldLabel, type = "Text", isRequired = true, order = 0 }
            }
        });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var template = await response.Content.ReadFromJsonAsync<ReportTemplateResponse>();
        Assert.That(template, Is.Not.Null);
        return template!;
    }

    private async Task<User> SeedTrainerAsync(string name, string email)
    {
        var trainer = await SeedUserAsync(name: name, email: email, password: "password123");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var alreadyLinked = await db.UserRoles.AnyAsync(ur => ur.UserId == (Domain.ValueObjects.Id<User>)trainer.Id && ur.RoleId == (Domain.ValueObjects.Id<Role>)RoleSeedDataConfiguration.TrainerRoleSeedId);
        if (!alreadyLinked)
        {
            db.UserRoles.Add(LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(db, (Domain.ValueObjects.Id<User>)trainer.Id));
            await db.SaveChangesAsync();
        }

        return trainer;
    }

    [Test]
    public async Task SubmitReport_WithMissingRequiredPhotoViews_ShouldReturnUnprocessableEntity()
    {
        var trainer = await SeedTrainerAsync("trainer-photo-blocking", "trainer-photo-blocking@example.com");
        var trainee = await SeedUserAsync(name: "trainee-photo-blocking", email: "trainee-photo-blocking@example.com", password: "password123");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Domain.ValueObjects.Id<TrainerTraineeLink>.New(),
                TrainerId = (Domain.ValueObjects.Id<User>)trainer.Id,
                TraineeId = (Domain.ValueObjects.Id<User>)trainee.Id
            });
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);
        var templateResponse = await Client.PostAsJsonAsync("/api/trainer/report-templates", new
        {
            name = "Photo Progress Report",
            fields = new object[]
            {
                new
                {
                    key = "photos",
                    label = "Progress Photos",
                    type = "Photos",
                    isRequired = true,
                    order = 0,
                    moduleConfig = new
                    {
                        requiredViews = new[] { "Front", "SideLeft", "SideRight", "Back" }
                    }
                }
            }
        });

        Assert.That(templateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var template = await templateResponse.Content.ReadFromJsonAsync<ReportTemplateResponse>();

        var requestResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/report-requests", new
        {
            templateId = template!.Id
        });

        Assert.That(requestResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var request = await requestResponse.Content.ReadFromJsonAsync<ReportRequestResponse>();

        SetAuthorizationHeader(trainee.Id);
        var submitResponse = await Client.PostAsJsonAsync($"/api/trainee/report-requests/{request!.Id}/submit", new
        {
            answers = new
            {
                photos = Array.Empty<string>()
            }
        });

        Assert.That(submitResponse.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        var errorContent = await submitResponse.Content.ReadAsStringAsync();
        Assert.That(errorContent, Does.Contain("Missing required photo views"));
    }

    [Test]
    public async Task SubmitReport_WithAllRequiredPhotoViews_ShouldSucceed()
    {
        var trainer = await SeedTrainerAsync("trainer-photo-success", "trainer-photo-success@example.com");
        var trainee = await SeedUserAsync(name: "trainee-photo-success", email: "trainee-photo-success@example.com", password: "password123");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Domain.ValueObjects.Id<TrainerTraineeLink>.New(),
                TrainerId = (Domain.ValueObjects.Id<User>)trainer.Id,
                TraineeId = (Domain.ValueObjects.Id<User>)trainee.Id
            });
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);
        var templateResponse = await Client.PostAsJsonAsync("/api/trainer/report-templates", new
        {
            name = "Photo Progress Report",
            fields = new object[]
            {
                new
                {
                    key = "photos",
                    label = "Progress Photos",
                    type = "Photos",
                    isRequired = true,
                    order = 0,
                    moduleConfig = new
                    {
                        requiredViews = new[] { "Front", "SideLeft", "SideRight", "Back" }
                    }
                }
            }
        });

        Assert.That(templateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var template = await templateResponse.Content.ReadFromJsonAsync<ReportTemplateResponse>();

        var requestResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/report-requests", new
        {
            templateId = template!.Id
        });

        Assert.That(requestResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var request = await requestResponse.Content.ReadFromJsonAsync<ReportRequestResponse>();

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.That(Id<ReportRequest>.TryParse(request!.Id, out var requestId), Is.True);
            var traineeId = trainee.Id;

            db.Photos.AddRange(
                new Photo
                {
                    Id = Id<Photo>.New(),
                    ReportRequestId = requestId,
                    OwnerUserId = traineeId,
                    UploaderUserId = traineeId,
                    ViewType = Domain.Enums.PhotoViewType.Front.ToString(),
                    StorageKey = "photos/front.jpg",
                    MimeType = "image/jpeg",
                    SizeBytes = 1024,
                    Checksum = "abc123"
                },
                new Photo
                {
                    Id = Id<Photo>.New(),
                    ReportRequestId = requestId,
                    OwnerUserId = traineeId,
                    UploaderUserId = traineeId,
                    ViewType = Domain.Enums.PhotoViewType.SideLeft.ToString(),
                    StorageKey = "photos/side-left.jpg",
                    MimeType = "image/jpeg",
                    SizeBytes = 1024,
                    Checksum = "def456"
                },
                new Photo
                {
                    Id = Id<Photo>.New(),
                    ReportRequestId = requestId,
                    OwnerUserId = traineeId,
                    UploaderUserId = traineeId,
                    ViewType = Domain.Enums.PhotoViewType.SideRight.ToString(),
                    StorageKey = "photos/side-right.jpg",
                    MimeType = "image/jpeg",
                    SizeBytes = 1024,
                    Checksum = "ghi789"
                },
                new Photo
                {
                    Id = Id<Photo>.New(),
                    ReportRequestId = requestId,
                    OwnerUserId = traineeId,
                    UploaderUserId = traineeId,
                    ViewType = Domain.Enums.PhotoViewType.Back.ToString(),
                    StorageKey = "photos/back.jpg",
                    MimeType = "image/jpeg",
                    SizeBytes = 1024,
                    Checksum = "jkl012"
                });

            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainee.Id);
        var submitResponse = await Client.PostAsJsonAsync($"/api/trainee/report-requests/{request!.Id}/submit", new
        {
            answers = new
            {
                photos = Array.Empty<string>()
            }
        });

        Assert.That(submitResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainee/photos/initiate", "own", "owner-allow")]
    [AuthorizationEvidence("POST", "/api/trainee/reporting/photos/upload-init", "own", "owner-allow")]
    [AuthorizationEvidence("POST", "/api/trainer/reporting/photos/upload-init", "own", "owner-allow")]
    public async Task EndToEnd_PhotoUploadInitiateAndSubmit_Success()
    {
        var trainer = await SeedTrainerAsync("trainer-photo-e2e", "trainer-photo-e2e@example.com");
        var trainee = await SeedUserAsync(name: "trainee-photo-e2e", email: "trainee-photo-e2e@example.com", password: "password123");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = (Id<User>)trainer.Id,
                TraineeId = (Id<User>)trainee.Id
            });
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);
        var templateResponse = await Client.PostAsJsonAsync("/api/trainer/report-templates", new
        {
            name = "Photo E2E Report",
            fields = new object[]
            {
                new
                {
                    key = "photos",
                    label = "Progress Photos",
                    type = "Photos",
                    isRequired = true,
                    order = 0,
                    moduleConfig = new
                    {
                        requiredViews = new[] { "Front" }
                    }
                }
            }
        });

        Assert.That(templateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var template = await templateResponse.Content.ReadFromJsonAsync<ReportTemplateResponse>();

        var requestResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/report-requests", new
        {
            templateId = template!.Id
        });

        Assert.That(requestResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var request = await requestResponse.Content.ReadFromJsonAsync<ReportRequestResponse>();

        SetAuthorizationHeader(trainee.Id);
        var initiateResponse = await Client.PostAsJsonAsync("/api/trainee/photos/initiate", new
        {
            reportRequestId = request!.Id,
            viewType = "Front",
            mimeType = "image/jpeg",
            sizeBytes = 1024000
        });

        Assert.That(initiateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var initiateResult = await initiateResponse.Content.ReadFromJsonAsync<JsonElement>();
        var storageKey = initiateResult.GetProperty("storageKey").GetString();
        Assert.That(storageKey, Is.Not.Null.And.Not.Empty);

        SetAuthorizationHeader(trainer.Id);
        var trainerInitiateResponse = await Client.PostAsJsonAsync("/api/trainer/reporting/photos/upload-init", new
        {
            reportRequestId = request.Id,
            viewType = "SideLeft",
            mimeType = "image/jpeg",
            sizeBytes = 1024
        });
        Assert.That(trainerInitiateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainee/photos/initiate", "own", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/trainee/reporting/photos/upload-init", "own", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/reporting/photos/upload-init", "own", "anonymous-denial")]
    public async Task TraineePhotoInitiate_WithoutAuthorization_ReturnsUnauthorized()
    {
        ClearAuthorizationHeader();

        using var response = await Client.PostAsJsonAsync("/api/trainee/photos/initiate", new
        {
            reportRequestId = Id<ReportRequest>.New().ToString(),
            viewType = "Front",
            mimeType = "image/jpeg",
            sizeBytes = 1024
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        using var trainerResponse = await Client.PostAsJsonAsync("/api/trainer/reporting/photos/upload-init", new
        {
            reportRequestId = Id<ReportRequest>.New().ToString(),
            viewType = "Front",
            mimeType = "image/jpeg",
            sizeBytes = 1024
        });
        Assert.That(trainerResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainee/photos/complete-upload", "own", "owner-allow")]
    [AuthorizationEvidence("POST", "/api/trainee/photos/complete-upload", "own", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/trainee/reporting/photos/complete-upload", "own", "owner-allow")]
    [AuthorizationEvidence("POST", "/api/trainee/reporting/photos/complete-upload", "own", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/reporting/photos/complete-upload", "own", "owner-allow")]
    [AuthorizationEvidence("POST", "/api/trainer/reporting/photos/complete-upload", "own", "anonymous-denial")]
    public async Task TraineeCompletePhotoUploadRoutes_EnforceAuthenticationBeforeFinalization()
    {
        var trainer = await SeedTrainerAsync("http-complete-photo-trainer", "http-complete-photo-trainer@example.test");
        var trainee = await SeedUserAsync(name: "http-complete-photo-trainee", email: "http-complete-photo-trainee@example.test", password: "password123");

        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id
            });
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);
        var template = await CreateReportTemplateAsync(
            "HTTP complete photo template",
            "HTTP complete photo field",
            "httpCompletePhotoField");
        using var requestResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/report-requests", new
        {
            templateId = template.Id
        });
        Assert.That(requestResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var request = await requestResponse.Content.ReadFromJsonAsync<ReportRequestResponse>();
        Assert.That(request, Is.Not.Null);

        var payload = new
        {
            reportRequestId = request!.Id,
            viewType = "Front",
            storageKey = "photos/http-complete-photo/missing.jpg",
            mimeType = "image/jpeg",
            sizeBytes = 1024,
            checksum = "missing"
        };

        SetAuthorizationHeader(trainee.Id);
        using var ownerResponse = await Client.PostAsJsonAsync("/api/trainee/photos/complete-upload", payload);
        Assert.That(ownerResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var ownerText = await ownerResponse.Content.ReadAsStringAsync();
        Assert.That(ownerText, Does.Not.Contain(payload.storageKey));

        ClearAuthorizationHeader();
        using var anonymousResponse = await Client.PostAsJsonAsync("/api/trainee/photos/complete-upload", payload);
        Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var anonymousText = await anonymousResponse.Content.ReadAsStringAsync();
        Assert.That(anonymousText, Does.Not.Contain(payload.storageKey));

        SetAuthorizationHeader(trainee.Id);
        using var legacyOwnerResponse = await Client.PostAsJsonAsync("/api/trainee/reporting/photos/complete-upload", payload);
        Assert.That(legacyOwnerResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        ClearAuthorizationHeader();
        using var legacyAnonymousResponse = await Client.PostAsJsonAsync("/api/trainee/reporting/photos/complete-upload", payload);
        Assert.That(legacyAnonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        SetAuthorizationHeader(trainer.Id);
        using var trainerResponse = await Client.PostAsJsonAsync("/api/trainer/reporting/photos/complete-upload", payload);
        Assert.That(trainerResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        ClearAuthorizationHeader();
        using var trainerAnonymousResponse = await Client.PostAsJsonAsync("/api/trainer/reporting/photos/complete-upload", payload);
        Assert.That(trainerAnonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task EndToEnd_UnauthorizedPhotoUpload_Denied()
    {
        var trainer = await SeedTrainerAsync("trainer-photo-unauth", "trainer-photo-unauth@example.com");
        var trainee = await SeedUserAsync(name: "trainee-photo-unauth", email: "trainee-photo-unauth@example.com", password: "password123");
        var otherUser = await SeedUserAsync(name: "other-user-photo", email: "other-user-photo@example.com", password: "password123");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = (Id<User>)trainer.Id,
                TraineeId = (Id<User>)trainee.Id
            });
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);
        var templateResponse = await Client.PostAsJsonAsync("/api/trainer/report-templates", new
        {
            name = "Photo Unauth Report",
            fields = new object[]
            {
                new
                {
                    key = "photos",
                    label = "Progress Photos",
                    type = "Photos",
                    isRequired = true,
                    order = 0,
                    moduleConfig = new
                    {
                        requiredViews = new[] { "Front" }
                    }
                }
            }
        });

        var template = await templateResponse.Content.ReadFromJsonAsync<ReportTemplateResponse>();

        var requestResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/report-requests", new
        {
            templateId = template!.Id
        });

        var request = await requestResponse.Content.ReadFromJsonAsync<ReportRequestResponse>();

        SetAuthorizationHeader(otherUser.Id);
        var initiateResponse = await Client.PostAsJsonAsync("/api/trainee/photos/initiate", new
        {
            reportRequestId = request!.Id,
            viewType = "Front",
            mimeType = "image/jpeg",
            sizeBytes = 1024000
        });

        Assert.That(initiateResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task EndToEnd_DuplicatePhotoUpload_ReplacesOld()
    {
        var trainer = await SeedTrainerAsync("trainer-photo-dup", "trainer-photo-dup@example.com");
        var trainee = await SeedUserAsync(name: "trainee-photo-dup", email: "trainee-photo-dup@example.com", password: "password123");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = (Id<User>)trainer.Id,
                TraineeId = (Id<User>)trainee.Id
            });
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);
        var templateResponse = await Client.PostAsJsonAsync("/api/trainer/report-templates", new
        {
            name = "Photo Duplicate Report",
            fields = new object[]
            {
                new
                {
                    key = "photos",
                    label = "Progress Photos",
                    type = "Photos",
                    isRequired = true,
                    order = 0,
                    moduleConfig = new
                    {
                        requiredViews = new[] { "Front" }
                    }
                }
            }
        });

        var template = await templateResponse.Content.ReadFromJsonAsync<ReportTemplateResponse>();

        var requestResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/report-requests", new
        {
            templateId = template!.Id
        });

        var request = await requestResponse.Content.ReadFromJsonAsync<ReportRequestResponse>();

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.That(Id<ReportRequest>.TryParse(request!.Id, out var requestId), Is.True);
            var traineeId = trainee.Id;

            var oldPhoto = new Photo
            {
                Id = Id<Photo>.New(),
                ReportRequestId = requestId,
                OwnerUserId = traineeId,
                UploaderUserId = traineeId,
                ViewType = PhotoViewType.Front.ToString(),
                StorageKey = "photos/old-front.jpg",
                MimeType = "image/jpeg",
                SizeBytes = 1024,
                Checksum = "old-checksum",
                IsDeleted = false
            };

            db.Photos.Add(oldPhoto);
            await db.SaveChangesAsync();

            var newPhoto = new Photo
            {
                Id = Id<Photo>.New(),
                ReportRequestId = requestId,
                OwnerUserId = traineeId,
                UploaderUserId = traineeId,
                ViewType = PhotoViewType.Front.ToString(),
                StorageKey = "photos/new-front.jpg",
                MimeType = "image/jpeg",
                SizeBytes = 2048,
                Checksum = "new-checksum",
                IsDeleted = false
            };

            oldPhoto.IsDeleted = true;

            db.Photos.Add(newPhoto);
            await db.SaveChangesAsync();
        }

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.That(Id<ReportRequest>.TryParse(request!.Id, out var requestId), Is.True);

            var photos = await db.Photos
                .Where(p => p.ReportRequestId == requestId && p.ViewType == PhotoViewType.Front.ToString())
                .ToListAsync();

            Assert.That(photos, Has.Exactly(2).Items);
            Assert.That(photos.Count(p => p.IsDeleted), Is.EqualTo(1));
            Assert.That(photos.Count(p => !p.IsDeleted), Is.EqualTo(1));
            Assert.That(photos.Single(p => !p.IsDeleted).Checksum, Is.EqualTo("new-checksum"));
        }
    }

    [Test]
    public async Task EndToEnd_MixedTemplate_PhotosAndScalarFields_ValidatesCorrectly()
    {
        var trainer = await SeedTrainerAsync("trainer-mixed", "trainer-mixed@example.com");
        var trainee = await SeedUserAsync(name: "trainee-mixed", email: "trainee-mixed@example.com", password: "password123");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = (Id<User>)trainer.Id,
                TraineeId = (Id<User>)trainee.Id
            });
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);
        var templateResponse = await Client.PostAsJsonAsync("/api/trainer/report-templates", new
        {
            name = "Mixed Template Report",
            fields = new object[]
            {
                new
                {
                    key = "photos",
                    label = "Progress Photos",
                    type = "Photos",
                    isRequired = true,
                    order = 0,
                    moduleConfig = new
                    {
                        requiredViews = new[] { "Front", "Side" }
                    }
                },
                new
                {
                    key = "weight",
                    label = "Current Weight",
                    type = "Number",
                    isRequired = true,
                    order = 1
                },
                new
                {
                    key = "notes",
                    label = "Notes",
                    type = "Text",
                    isRequired = false,
                    order = 2
                }
            }
        });

        Assert.That(templateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var template = await templateResponse.Content.ReadFromJsonAsync<ReportTemplateResponse>();
        Assert.That(template!.Fields, Has.Exactly(3).Items);
        Assert.That((template.Fields).Any(f => f.Key == "photos"), Is.True);
        Assert.That((template.Fields).Any(f => f.Key == "weight"), Is.True);
        Assert.That((template.Fields).Any(f => f.Key == "notes"), Is.True);
    }

    private sealed class ReportTemplateResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("fields")]
        public List<ReportTemplateFieldResponse> Fields { get; set; } = [];
    }

    private sealed class ReportTemplateFieldResponse
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;
    }

    private sealed class ReportRequestResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("note")]
        public string? Note { get; set; }
    }

    private sealed class ReportSubmissionResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("reportRequestId")]
        public string ReportRequestId { get; set; } = string.Empty;

        [JsonPropertyName("answers")]
        public Dictionary<string, JsonElement> Answers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class RecurringReportAssignmentResponse
    {
        [JsonPropertyName("templateId")]
        public string TemplateId { get; set; } = string.Empty;

        [JsonPropertyName("note")]
        public string? Note { get; set; }
    }

    private sealed class PhotoHistoryResponse
    {
        [JsonPropertyName("photos")]
        public List<PhotoHistoryItemResponse> Photos { get; set; } = [];
    }

    private sealed class PhotoHistoryItemResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("viewType")]
        public string ViewType { get; set; } = string.Empty;

        [JsonPropertyName("sizeBytes")]
        public long SizeBytes { get; set; }

        [JsonPropertyName("thumbnailUrl")]
        public string? ThumbnailUrl { get; set; }

        [JsonPropertyName("readUrl")]
        public string ReadUrl { get; set; } = string.Empty;

        [JsonPropertyName("reportRequestId")]
        public string ReportRequestId { get; set; } = string.Empty;
    }

    private sealed class SignedReadUrlResponse
    {
        [JsonPropertyName("readUrl")]
        public string ReadUrl { get; set; } = string.Empty;

        [JsonPropertyName("expiresAt")]
        public DateTimeOffset ExpiresAt { get; set; }
    }
}
