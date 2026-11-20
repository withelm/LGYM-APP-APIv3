using System.Net;
using Hangfire;
using LgymApi.Application.Abstractions.Storage;
using LgymApi.Application.Coaching.ApiAdapters;
using LgymApi.Application.Coaching.ManagedPlans;
using LgymApi.Application.Identity.ApiAdapters;
using LgymApi.Application.Nutrition.ApiAdapters;
using LgymApi.Application.Platform.ReferenceData.ApiAdapters;
using LgymApi.Application.Platform.ReferenceData.AppConfig;
using LgymApi.Application.Platform.ReferenceData.AppConfig.Contracts;
using LgymApi.Application.Platform.ReferenceData.Enums;
using LgymApi.Application.Platform.ReferenceData.Units;
using LgymApi.Application.Features.PasswordReset.Contracts;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Notifications.Contracts.Events;
using LgymApi.Application.Notifications.Contracts.Push;
using LgymApi.Application.Pagination;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.Repositories;
using LgymApi.Application.Reporting.ApiAdapters;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Application.Services;
using LgymApi.Application.TrainingPlanning.ApiAdapters;
using LgymApi.Application.WorkoutProgress.Adapters;
using LgymApi.Application.WorkoutProgress.ApiAdapters;
using LgymApi.BackgroundWorker;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Notifications;
using LgymApi.BackgroundWorker.Runtime;
using LgymApi.BackgroundWorker.Services;
using LgymApi.Notifications;
using LgymApi.Notifications.ApiAdapters;
using LgymApi.Domain.Enums;
using LgymApi.Infrastructure.Pagination;
using LgymApi.Infrastructure.Repositories;
using LgymApi.Infrastructure.Repositories.Reporting;
using LgymApi.Infrastructure.Repositories.ReferenceData;
using LgymApi.Infrastructure.Services;
using LgymApi.Infrastructure.UnitOfWork;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ApplicationMapper = LgymApi.Application.Mapping.Core.IMapper;
using QueryPaginationFacade = LgymApi.Infrastructure.Pagination.QueryPaginationService;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class CompositionRootStartupTests : IntegrationTestBase
{
    private static readonly Type[] ApplicationApiAdapterContracts =
    [
        typeof(IAuthenticatedAccountApiAdapter),
        typeof(IAccountAccessApiAdapter),
        typeof(IAccountEloApiAdapter),
        typeof(IAccountExternalLoginApiAdapter),
        typeof(IAccountTutorialApiAdapter),
        typeof(IAdminAccountManagementApiAdapter),
        typeof(IRoleManagementApiAdapter),
        typeof(IPlanAccountApiAdapter),
        typeof(IManagedPlanAccountApiAdapter),
        typeof(IDietPlanAccountApiAdapter),
        typeof(ISupplementationApiAdapter),
        typeof(IExerciseApiAdapter),
        typeof(IMainRecordsApiAdapter),
        typeof(IAppConfigApiAdapter),
        typeof(ITrainerInvitationApiPort),
        typeof(ITrainerDashboardProgressApiPort),
        typeof(ITrainerTraineeNotesApiPort),
        typeof(ITraineeNotesApiPort),
        typeof(ITraineeRelationshipApiPort),
        typeof(ITrainerReportTemplateApiPort),
        typeof(ITrainerReportRequestApiPort),
        typeof(ITraineeReportRequestApiPort),
        typeof(ITrainerReportPhotoApiPort),
        typeof(ITraineeReportPhotoApiPort),
        typeof(IRecurringReportAssignmentApiPort)
    ];

    private static readonly Type[] NotificationsApiAdapterContracts =
    [
        typeof(IInAppNotificationApiAdapter),
        typeof(INotificationEventApiAdapter),
        typeof(IPushInstallationApiAdapter)
    ];

    [Test]
    public void Worker_Facade_Should_Remain_The_Composition_Entry_Point()
    {
        Assert.That(typeof(BackgroundWorkerRecurringJobs).Assembly.GetName().Name, Is.EqualTo("LgymApi.BackgroundWorker"));
    }

    [Test]
    public void TestingApiHost_ResolvesCanonicalPlatformAndProviderServices()
    {
        using var scope = Factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        var assertedValue10 = (services.GetServices<IAppConfigService>()).ToArray();
        Assert.That(assertedValue10, Has.Exactly(1).Items);
        Assert.That(assertedValue10.Single(), Is.TypeOf<AppConfigService>());
        var assertedValue11 = (services.GetServices<IAppConfigAuthorizationPort>()).ToArray();
        Assert.That(assertedValue11, Has.Exactly(1).Items);
        Assert.That(assertedValue11.Single().GetType().FullName, Is.EqualTo("LgymApi.Application.Identity.Adapters.AppConfigAuthorizationAdapter"));
        var assertedValue12 = (services.GetServices<IEnumService>()).ToArray();
        Assert.That(assertedValue12, Has.Exactly(1).Items);
        Assert.That(assertedValue12.Single(), Is.TypeOf<EnumService>());
        var assertedValue13 = (services.GetServices<IUnitConverter<WeightUnits>>()).ToArray();
        Assert.That(assertedValue13, Has.Exactly(1).Items);
        Assert.That(assertedValue13.Single(), Is.TypeOf<LinearUnitConverter<WeightUnits>>());
        var assertedValue14 = (services.GetServices<IUnitConverter<HeightUnits>>()).ToArray();
        Assert.That(assertedValue14, Has.Exactly(1).Items);
        Assert.That(assertedValue14.Single(), Is.TypeOf<LinearUnitConverter<HeightUnits>>());
        var assertedValue15 = (services.GetServices<IAppConfigRepository>()).ToArray();
        Assert.That(assertedValue15, Has.Exactly(1).Items);
        Assert.That(assertedValue15.Single(), Is.TypeOf<AppConfigRepository>());
        var assertedValue16 = (services.GetServices<ICommittedIntentDispatcher>()).ToArray();
        Assert.That(assertedValue16, Has.Exactly(1).Items);
        Assert.That(assertedValue16.Single(), Is.TypeOf<CommittedIntentDispatcher>());
        var assertedValue17 = (services.GetServices<IUnitOfWork>()).ToArray();
        Assert.That(assertedValue17, Has.Exactly(1).Items);
        Assert.That(assertedValue17.Single(), Is.TypeOf<EfUnitOfWork>());
        var assertedValue18 = (services.GetServices<ICommandEnvelopeRepository>()).ToArray();
        Assert.That(assertedValue18, Has.Exactly(1).Items);
        Assert.That(assertedValue18.Single(), Is.TypeOf<CommandEnvelopeRepository>());
        var assertedValue19 = (services.GetServices<IApiIdempotencyRecordRepository>()).ToArray();
        Assert.That(assertedValue19, Has.Exactly(1).Items);
        Assert.That(assertedValue19.Single(), Is.TypeOf<ApiIdempotencyRecordRepository>());
        var assertedValue20 = (services.GetServices<IEmailNotificationsFeature>()).ToArray();
        Assert.That(assertedValue20, Has.Exactly(1).Items);
        Assert.That(assertedValue20.Single(), Is.TypeOf<EmailNotificationsFeature>());
        var assertedValue21 = (services.GetServices<IEmailMetrics>()).ToArray();
        Assert.That(assertedValue21, Has.Exactly(1).Items);
        Assert.That(assertedValue21.Single(), Is.TypeOf<EmailMetrics>());
        var assertedValue22 = (services.GetServices<IEmailTemplateComposerFactory>()).ToArray();
        Assert.That(assertedValue22, Has.Exactly(1).Items);
        Assert.That(assertedValue22.Single(), Is.TypeOf<EmailTemplateComposerFactory>());
        var assertedValue23 = (services.GetServices<ApplicationMapper>()).ToArray();
        Assert.That(assertedValue23, Has.Exactly(1).Items);
        Assert.That(assertedValue23.Single(), Is.TypeOf<Mapper>());
        var assertedValue24 = (services.GetServices<IMapperRegistry>()).ToArray();
        Assert.That(assertedValue24, Has.Exactly(1).Items);
        Assert.That(assertedValue24.Single(), Is.TypeOf<MapperRegistry>());
        var assertedValue25 = (services.GetServices<IQueryPaginationService>()).ToArray();
        Assert.That(assertedValue25, Has.Exactly(1).Items);
        Assert.That(assertedValue25.Single(), Is.TypeOf<QueryPaginationFacade>());
        var assertedValue26 = (services.GetServices<PaginationPolicy>()).ToArray();
        Assert.That(assertedValue26, Has.Exactly(1).Items);
        Assert.That(assertedValue26.Single(), Is.EqualTo(new PaginationPolicy
            {
                MaxPageSize = 100,
                DefaultPageSize = 20,
                DefaultSortField = "id",
                TieBreakerField = "id"
            }).UsingPropertiesComparer());
        Assert.That(services.GetServices<CommandContractRegistry>(), Has.Exactly(1).Items);
        var assertedValue27 = (services.GetServices<ICommandDispatcher>()).ToArray();
        Assert.That(assertedValue27, Has.Exactly(1).Items);
        Assert.That(assertedValue27.Single(), Is.TypeOf<CommandDispatcher>());
        var assertedValue28 = (services.GetServices<IPasswordRecoveryEmailScheduler>()).ToArray();
        Assert.That(assertedValue28, Has.Exactly(1).Items);
        Assert.That(assertedValue28.Single(), Is.TypeOf<PasswordRecoveryEmailSchedulerAdapter>());
        var assertedValue29 = (services.GetServices<ICoachingEmailNotificationFeature>()).ToArray();
        Assert.That(assertedValue29, Has.Exactly(1).Items);
        Assert.That(assertedValue29.Single(), Is.TypeOf<CoachingEmailNotificationSchedulerAdapter>());
        var assertedValue30 = (services.GetServices<ICoachingEmailNotificationScheduler>()).ToArray();
        Assert.That(assertedValue30, Has.Exactly(1).Items);
        Assert.That(assertedValue30.Single(), Is.TypeOf<CoachingEmailNotificationSchedulerAdapter>());
        var assertedValue31 = (services.GetServices<IPushBackgroundScheduler>()).ToArray();
        Assert.That(assertedValue31, Has.Exactly(1).Items);
        Assert.That(assertedValue31.Single(), Is.TypeOf<NoOpPushBackgroundScheduler>());
        var assertedValue32 = (services.GetServices<IPushProviderSender>()).ToArray();
        Assert.That(assertedValue32, Has.Exactly(1).Items);
        Assert.That(assertedValue32.Single(), Is.SameAs(Factory.PushSender));
        var assertedValue33 = (services.GetServices<IPhotoStorageProvider>()).ToArray();
        Assert.That(assertedValue33, Has.Exactly(1).Items);
        Assert.That(assertedValue33.Single(), Is.TypeOf<LocalPhotoStorageProvider>());
        var assertedValue34 = (services.GetServices<IReportPhotoPersistence>()).ToArray();
        Assert.That(assertedValue34, Has.Exactly(1).Items);
        Assert.That(assertedValue34.Single(), Is.TypeOf<ReportPhotoPersistenceRepository>());
        var assertedValue35 = (services.GetServices<ITokenService>()).ToArray();
        Assert.That(assertedValue35, Has.Exactly(1).Items);
        Assert.That(assertedValue35.Single(), Is.TypeOf<TokenService>());
        var assertedValue36 = (services.GetServices<IGoogleTokenValidator>()).ToArray();
        Assert.That(assertedValue36, Has.Exactly(1).Items);
        Assert.That(assertedValue36.Single(), Is.TypeOf<GoogleTokenValidator>());
        var assertedValue37 = (services.GetServices<ILegacyPasswordService>()).ToArray();
        Assert.That(assertedValue37, Has.Exactly(1).Items);
        Assert.That(assertedValue37.Single(), Is.TypeOf<LegacyPasswordService>());
        var assertedValue38 = (services.GetServices<IUserSessionStore>()).ToArray();
        Assert.That(assertedValue38, Has.Exactly(1).Items);
        Assert.That(assertedValue38.Single(), Is.TypeOf<UserSessionStore>());
        var assertedValue39 = (services.GetServices<LgymApi.Application.Identity.Contracts.Sessions.IAccountSessionDisassociationPort>()).ToArray();
        Assert.That(assertedValue39, Has.Exactly(1).Items);
        Assert.That(assertedValue39.Single()
            .GetType()
            .FullName, Is.EqualTo("LgymApi.Application.Notifications.Adapters.PushInstallationSessionDisassociationAdapter"));
        var assertedValue40 = (services.GetServices<IEmailSender>()).ToArray();
        Assert.That(assertedValue40, Has.Exactly(1).Items);
        Assert.That(assertedValue40.Single(), Is.SameAs(Factory.EmailSender));
        var emailTemplateComposers = services.GetServices<IEmailTemplateComposer>().ToArray();
        Assert.That(emailTemplateComposers, Has.Exactly(7).Items);
        Assert.That(emailTemplateComposers.Select(composer => composer.GetType()), Is.Unique);
        var mappingProfiles = services.GetServices<IMappingProfile>().ToArray();
        Assert.That(mappingProfiles, Has.Exactly(49).Items);
        Assert.That(mappingProfiles.Select(profile => profile.GetType()), Is.Unique);
        Assert.That(mappingProfiles.OfType<EnumLookupMappingProfile>(), Has.Exactly(1).Items);
        Assert.That(mappingProfiles.OfType<PlanExerciseWorkoutAdapterMappingProfile>(), Has.Exactly(1).Items);
        Assert.That(mappingProfiles.OfType<ManagedPlanCollaborationMappingProfile>(), Has.Exactly(1).Items);
        Assert.That(mappingProfiles.OfType<IdentityApiAdapterMappingProfile>(), Has.Exactly(1).Items);
        Assert.That(mappingProfiles.OfType<NutritionApiAdapterMappingProfile>(), Has.Exactly(1).Items);
        Assert.That(mappingProfiles.OfType<PlanApiAdapterMappingProfile>(), Has.Exactly(1).Items);
        Assert.That(mappingProfiles.OfType<CoachingApiAdapterMappingProfile>(), Has.Exactly(1).Items);
        Assert.That(mappingProfiles.OfType<ReportingApiAdapterMappingProfile>(), Has.Exactly(1).Items);
        AssertApiAdapterContractsResolveExactlyOnce(services, ApplicationApiAdapterContracts, expectedCount: 25);
        AssertApiAdapterContractsResolveExactlyOnce(services, NotificationsApiAdapterContracts, expectedCount: 3);
        Assert.That(services.GetService<JobStorage>(), Is.Null);
    }

    [Test]
    public async Task TestingApiHost_StartsWithTestingAndProductionSafeProviderConfigurations()
    {
        using var baseFactory = new CustomWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Email:DeliveryMode", "Smtp");
            builder.UseSetting("PhotoStorage:Provider", "CloudflareR2");
            builder.UseSetting("PhotoStorage:BucketName", "lgym-report-photos-test");
            builder.UseSetting("PhotoStorage:Endpoint", "https://example.r2.cloudflarestorage.com");
            builder.UseSetting("PhotoStorage:AccessKeyId", "test-access-key");
            builder.UseSetting("PhotoStorage:SecretAccessKey", "test-secret-key");
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope();
        var assertedValue41 = (scope.ServiceProvider.GetServices<IEmailSender>()).ToArray();
        Assert.That(assertedValue41, Has.Exactly(1).Items);
        Assert.That(assertedValue41.Single(), Is.SameAs(baseFactory.EmailSender));
        var assertedValue42 = (scope.ServiceProvider.GetServices<IPhotoStorageProvider>()).ToArray();
        Assert.That(assertedValue42, Has.Exactly(1).Items);
        Assert.That(assertedValue42.Single(), Is.TypeOf<CloudflareR2PhotoStorageProvider>());
        var assertedValue43 = (scope.ServiceProvider.GetServices<IPushBackgroundScheduler>()).ToArray();
        Assert.That(assertedValue43, Has.Exactly(1).Items);
        Assert.That(assertedValue43.Single(), Is.TypeOf<NoOpPushBackgroundScheduler>());
        var assertedValue44 = (scope.ServiceProvider.GetServices<IPushProviderSender>()).ToArray();
        Assert.That(assertedValue44, Has.Exactly(1).Items);
        Assert.That(assertedValue44.Single(), Is.SameAs(baseFactory.PushSender));
        Assert.That(scope.ServiceProvider.GetService<JobStorage>(), Is.Null);
    }

    [Test]
    public async Task TestingApiHost_DoesNotExposeDevelopmentOnlySwaggerOrHangfireMiddleware()
    {
        var user = await SeedUserAsync("technical-middleware-probe", "technical-middleware-probe@example.test");
        SetAuthorizationHeader(user.Id);

        foreach (var path in new[] { "/swagger/v1/swagger.json", "/swagger/index.html", "/hangfire" })
        {
            using var response = await Client.GetAsync(path);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound), $"{path} is not part of the Testing host");
        }
    }

    [TestCase("Email:DeliveryMode", "invalid", "Email:DeliveryMode must be one of: Smtp, Dummy.")]
    [TestCase("PhotoStorage:Provider", "Unsupported", "Unsupported photo storage provider: Unsupported")]
    public void TestingApiHost_RejectsInvalidProviderConfigurationDuringStartup(
        string configurationKey,
        string configurationValue,
        string expectedMessage)
    {
        using var baseFactory = new CustomWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.UseSetting(configurationKey, configurationValue));

        var action = () =>
        {
            using var client = factory.CreateClient();
        };

        Assert.That(Assert.Catch<Exception>(() => { action(); })!.ToString(), Does.Contain(expectedMessage));
    }

    private static void AssertApiAdapterContractsResolveExactlyOnce(
        IServiceProvider services,
        IReadOnlyCollection<Type> contracts,
        int expectedCount)
    {
        Assert.That(contracts, Has.Exactly(expectedCount).Items);
        Assert.That(contracts, Is.Unique);

        foreach (var contract in contracts)
        {
            var instances = services.GetServices(contract).ToArray();
            Assert.That(instances, Has.Exactly(1).Items, contract.FullName);
            Assert.That(instances.Single(), Is.Not.Null, contract.FullName);
        }
    }
}
