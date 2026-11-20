using Hangfire;
using Hangfire.Logging;
using LgymApi.BackgroundWorker.Jobs;
using LgymApi.Api.Hubs;
using LgymApi.Application;
using LgymApi.Application.Abstractions.Storage;
using LgymApi.Application.Coaching.ManagedPlans;
using LgymApi.Application.Identity.Adapters;
using LgymApi.Application.Identity.ApiAdapters;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Application.Nutrition.ApiAdapters;
using LgymApi.Application.Platform.ReferenceData.AppConfig;
using LgymApi.Application.Platform.ReferenceData.AppConfig.Contracts;
using LgymApi.Application.Platform.ReferenceData.Enums;
using LgymApi.Application.Platform.ReferenceData.Units;
using LgymApi.Application.Features.PasswordReset.Contracts;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Contracts.Events;
using LgymApi.Application.Notifications.Contracts.Push;
using LgymApi.Application.Notifications.Providers.Fcm;
using LgymApi.Application.Options;
using LgymApi.Application.Pagination;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.Repositories;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Application.Reporting;
using LgymApi.Application.Services;
using LgymApi.Application.TrainingPlanning.ApiAdapters;
using LgymApi.Application.WorkoutProgress.Adapters;
using LgymApi.BackgroundWorker;
using LgymApi.BackgroundWorker.Actions.Contracts;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.BackgroundWorker.Notifications;
using LgymApi.Notifications;
using LgymApi.BackgroundWorker.Runtime;
using LgymApi.BackgroundWorker.Services;
using LgymApi.Infrastructure;
using LgymApi.Infrastructure.Options;
using LgymApi.Infrastructure.Pagination;
using LgymApi.Infrastructure.Repositories;
using LgymApi.Infrastructure.Repositories.Reporting;
using LgymApi.Infrastructure.Repositories.ReferenceData;
using LgymApi.Infrastructure.Services;
using LgymApi.Infrastructure.UnitOfWork;
using LgymApi.Domain.Enums;
using LgymApi.Identity;
using LgymApi.Platform;
using LgymApi.TestUtils;
using LgymApi.TrainingPlanning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using ApplicationMapper = LgymApi.Application.Mapping.Core.IMapper;
using QueryPaginationFacade = LgymApi.Infrastructure.Pagination.QueryPaginationService;

namespace LgymApi.UnitTests;

[TestFixture]
[NonParallelizable]
public sealed class CompositionRootConvergenceTests
{
    private static readonly MethodInfo ResolveLogProvider = typeof(LogProvider).GetMethod(
        "ResolveLogProvider",
        BindingFlags.Static | BindingFlags.NonPublic)!;

    [TestCase(true, typeof(NoOpPushBackgroundScheduler))]
    [TestCase(false, typeof(HangfirePushBackgroundScheduler))]
    public void HostEquivalentComposition_RegistersExactCentralDescriptorsAndHandlers(
        bool isTesting,
        Type expectedSchedulerType)
    {
        var services = new ServiceCollection();
        var configuration = TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test"
        });

        services.AddNotificationsModule(configuration);
        services.AddBackgroundWorkerServices(isTesting);

        var action = () => ValidateCentralComposition(services, expectedSchedulerType);

        Assert.That(() => { action(); }, Throws.Nothing);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void FullHostEquivalentComposition_RegistersAndResolvesPlatformDescriptors(bool isTesting)
    {
        var services = CreateFullHostComposition(isTesting);

        ValidateFullHostComposition(services, isTesting);

        WithRestoredHangfireLogProvider(services, provider =>
        {
            using var scope = provider.CreateScope();
            var scopedServices = scope.ServiceProvider;

            Assert.That(scopedServices.GetRequiredService<IAppConfigService>(), Is.TypeOf<AppConfigService>());
            Assert.That(scopedServices.GetRequiredService<IAppConfigAuthorizationPort>(), Is.TypeOf<AppConfigAuthorizationAdapter>());
            Assert.That(scopedServices.GetRequiredService<IEnumService>(), Is.TypeOf<EnumService>());
            Assert.That(provider.GetRequiredService<IUnitConverter<WeightUnits>>(), Is.TypeOf<LinearUnitConverter<WeightUnits>>());
            Assert.That(provider.GetRequiredService<IUnitConverter<HeightUnits>>(), Is.TypeOf<LinearUnitConverter<HeightUnits>>());
            Assert.That(scopedServices.GetRequiredService<IAppConfigRepository>(), Is.TypeOf<AppConfigRepository>());
            Assert.That(scopedServices.GetRequiredService<ICommittedIntentDispatcher>(), Is.TypeOf<CommittedIntentDispatcher>());
            Assert.That(scopedServices.GetRequiredService<IUnitOfWork>(), Is.TypeOf<EfUnitOfWork>());
            Assert.That(scopedServices.GetRequiredService<ICommandEnvelopeRepository>(), Is.TypeOf<CommandEnvelopeRepository>());
            Assert.That(scopedServices.GetRequiredService<IApiIdempotencyRecordRepository>(), Is.TypeOf<ApiIdempotencyRecordRepository>());
            Assert.That(provider.GetRequiredService<IEmailNotificationsFeature>(), Is.TypeOf<EmailNotificationsFeature>());
            Assert.That(provider.GetRequiredService<IEmailMetrics>(), Is.TypeOf<EmailMetrics>());
            Assert.That(scopedServices.GetRequiredService<IEmailTemplateComposerFactory>(), Is.TypeOf<EmailTemplateComposerFactory>());
            Assert.That(provider.GetRequiredService<ApplicationMapper>(), Is.TypeOf<Mapper>());
            Assert.That(provider.GetRequiredService<IMapperRegistry>(), Is.TypeOf<MapperRegistry>());
            Assert.That(provider.GetRequiredService<PaginationPolicy>(), Is.EqualTo(new PaginationPolicy
            {
                MaxPageSize = 100,
                DefaultPageSize = 20,
                DefaultSortField = "id",
                TieBreakerField = "id"
            }).UsingPropertiesComparer());
            Assert.That(scopedServices.GetRequiredService<IQueryPaginationService>(), Is.TypeOf<QueryPaginationFacade>());

            var expectedEmailSenderType = isTesting ? typeof(DummyEmailSender) : typeof(SmtpEmailSender);
            var expectedPhotoStorageType = isTesting ? typeof(LocalPhotoStorageProvider) : typeof(CloudflareR2PhotoStorageProvider);
            var expectedEmailSchedulerType = isTesting ? typeof(NoOpEmailBackgroundScheduler) : typeof(HangfireEmailBackgroundScheduler);
            var expectedActionSchedulerType = isTesting ? typeof(NoOpActionMessageScheduler) : typeof(HangfireActionMessageScheduler);
            var expectedPushSchedulerType = isTesting ? typeof(NoOpPushBackgroundScheduler) : typeof(HangfirePushBackgroundScheduler);

            Assert.That(scopedServices.GetRequiredService<IEmailSender>(), Is.TypeOf(expectedEmailSenderType));
            Assert.That(scopedServices.GetRequiredService<IPhotoStorageProvider>(), Is.TypeOf(expectedPhotoStorageType));
            Assert.That(scopedServices.GetRequiredService<IReportPhotoPersistence>(), Is.TypeOf<ReportPhotoPersistenceRepository>());
            Assert.That(scopedServices.GetRequiredService<IPushProviderSender>(), Is.TypeOf<FcmPushSender>());
            Assert.That(scopedServices.GetRequiredService<IEmailBackgroundScheduler>(), Is.TypeOf(expectedEmailSchedulerType));
            Assert.That(scopedServices.GetRequiredService<IActionMessageScheduler>(), Is.TypeOf(expectedActionSchedulerType));
            Assert.That(scopedServices.GetRequiredService<IPushBackgroundScheduler>(), Is.TypeOf(expectedPushSchedulerType));
            Assert.That(scopedServices.GetRequiredService<ITokenService>(), Is.TypeOf<TokenService>());
            Assert.That(scopedServices.GetRequiredService<IGoogleTokenValidator>(), Is.TypeOf<GoogleTokenValidator>());
            Assert.That(scopedServices.GetRequiredService<ILegacyPasswordService>(), Is.TypeOf<LegacyPasswordService>());
            Assert.That(scopedServices.GetRequiredService<IUserSessionStore>(), Is.TypeOf<UserSessionStore>());
            Assert.That(scopedServices.GetRequiredService<IInAppNotificationPushPublisher>(), Is.TypeOf<LgymApi.Api.Features.InAppNotification.SignalRNotificationPushPublisher>());

            if (isTesting)
            {
                Assert.That(provider.GetService<JobStorage>(), Is.Null);
            }
            else
            {
                Assert.That(provider.GetRequiredService<JobStorage>().GetType().FullName, Is.EqualTo("Hangfire.PostgreSql.PostgreSqlStorage"));
            }
        });
    }

    [Test]
    public void DisposedNonTestingFullHostProvider_DoesNotLeaveHangfireGlobalLoggingProvider()
    {
        var services = CreateFullHostComposition(isTesting: false);

        WithRestoredHangfireLogProvider(services, provider => provider.GetRequiredService<JobStorage>());

        var action = () => typeof(ActionMessageJob).GetCustomAttribute<AutomaticRetryAttribute>();

        Assert.That(() => { action(); }, Throws.Nothing);
    }

    [Test]
    public void FullHostDescriptorValidation_RejectsMissingRegistration()
    {
        var services = CreateFullHostComposition(isTesting: true);
        services.Remove(services.Single(descriptor => descriptor.ServiceType == typeof(IAppConfigRepository)));

        var action = () => ValidateFullHostComposition(services, isTesting: true);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape($"*{typeof(IAppConfigRepository).FullName}*exactly once*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void FullHostDescriptorValidation_RejectsMissingAppConfigAuthorizationAdapter()
    {
        var services = CreateFullHostComposition(isTesting: true);
        services.Remove(services.Single(descriptor => descriptor.ServiceType == typeof(IAppConfigAuthorizationPort)));

        var action = () => ValidateFullHostComposition(services, isTesting: true);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape($"*{typeof(IAppConfigAuthorizationPort).FullName}*exactly once*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void FullHostDescriptorValidation_RejectsMissingEmailNotificationsFeatureRegistration()
    {
        var services = CreateFullHostComposition(isTesting: true);
        services.Remove(services.Single(descriptor => descriptor.ServiceType == typeof(IEmailNotificationsFeature)));

        var action = () => ValidateFullHostComposition(services, isTesting: true);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape($"*{typeof(IEmailNotificationsFeature).FullName}*exactly once*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void FullHostDescriptorValidation_RejectsDuplicateEmailMetricsRegistration()
    {
        var services = CreateFullHostComposition(isTesting: true);
        services.AddSingleton<IEmailMetrics, EmailMetrics>();

        var action = () => ValidateFullHostComposition(services, isTesting: true);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape($"*{typeof(IEmailMetrics).FullName}*exactly once*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void FullHostDescriptorValidation_RejectsDuplicateEmailSenderRegistration()
    {
        var services = CreateFullHostComposition(isTesting: true);
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        var action = () => ValidateFullHostComposition(services, isTesting: true);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape($"*{typeof(IEmailSender).FullName}*exactly once*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void FullHostDescriptorValidation_RejectsFactoryBackedTemplateComposerFactory()
    {
        var services = CreateFullHostComposition(isTesting: true);
        services.Remove(services.Single(descriptor => descriptor.ServiceType == typeof(IEmailTemplateComposerFactory)));
        services.AddScoped<IEmailTemplateComposerFactory>(_ => null!);

        var action = () => ValidateFullHostComposition(services, isTesting: true);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape($"*{typeof(IEmailTemplateComposerFactory).FullName}*"
                + $"*{typeof(EmailTemplateComposerFactory).FullName}*factory or instance*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void FullHostDescriptorValidation_RejectsInstanceBackedJobStorage()
    {
        var services = CreateFullHostComposition(isTesting: false);
        WithRestoredHangfireLogProvider(services, provider =>
        {
            var jobStorage = provider.GetRequiredService<JobStorage>();
            services.Remove(services.Single(descriptor => descriptor.ServiceType == typeof(JobStorage)));
            services.AddSingleton(jobStorage);
        });

        var action = () => ValidateFullHostComposition(services, isTesting: false);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape($"*{typeof(JobStorage).FullName}*factory*instance*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void FullHostDescriptorValidation_RejectsHangfireStorageInTesting()
    {
        var services = CreateFullHostComposition(isTesting: true);
        services.AddSingleton<JobStorage>(_ => null!);

        var action = () => ValidateFullHostComposition(services, isTesting: true);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape($"*Testing*{typeof(JobStorage).FullName}*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void FullHostDescriptorValidation_RejectsDuplicateFactoryRegistration()
    {
        var services = CreateFullHostComposition(isTesting: true);
        services.AddSingleton<ApplicationMapper>(_ => null!);

        var action = () => ValidateFullHostComposition(services, isTesting: true);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape($"*{typeof(ApplicationMapper).FullName}*exactly once*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void CentralDescriptorValidation_RejectsMissingRegistration()
    {
        var services = CreateExpectedCentralComposition(isTesting: true);
        services.Remove(services.Single(descriptor =>
            descriptor.ServiceType == typeof(IPasswordRecoveryEmailScheduler)));

        var action = () => ValidateCentralComposition(services, typeof(NoOpPushBackgroundScheduler));

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape($"*{typeof(IPasswordRecoveryEmailScheduler).FullName}*exactly once*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void CentralDescriptorValidation_RejectsDuplicateRegistration()
    {
        var services = CreateExpectedCentralComposition(isTesting: true);
        services.AddScoped(typeof(ICommandDispatcher), typeof(CommandDispatcher));

        var action = () => ValidateCentralComposition(services, typeof(NoOpPushBackgroundScheduler));

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape($"*{typeof(ICommandDispatcher).FullName}*exactly once*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void CentralDescriptorValidation_RejectsWrongEnvironmentSchedulerBranch()
    {
        var services = CreateExpectedCentralComposition(isTesting: true);
        services.Remove(services.Single(descriptor => descriptor.ServiceType == typeof(IPushBackgroundScheduler)));
        services.AddScoped<IPushBackgroundScheduler, HangfirePushBackgroundScheduler>();

        var action = () => ValidateCentralComposition(services, typeof(NoOpPushBackgroundScheduler));

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape($"*{typeof(IPushBackgroundScheduler).FullName}*{typeof(NoOpPushBackgroundScheduler).FullName}*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    private static void WithRestoredHangfireLogProvider(
        IServiceCollection services,
        Action<IServiceProvider> assertion)
    {
        var originalLogProvider = (ILogProvider)ResolveLogProvider.Invoke(null, null)!;
        try
        {
            using (var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            }))
            {
                assertion(provider);
            }
        }
        finally
        {
            LogProvider.SetCurrentLogProvider(originalLogProvider);
        }
    }

    private static ServiceCollection CreateExpectedCentralComposition(bool isTesting)
    {
        var services = new ServiceCollection();
        var configuration = TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test"
        });
        services.AddNotificationsModule(configuration);
        var registry = CommandContractRegistry.CreateDefault();
        services.AddSingleton(registry);
        services.AddScoped(typeof(ICommandDispatcher), typeof(CommandDispatcher));
        services.AddScoped<IBackgroundActionResolver, BackgroundActionResolver>();
        services.AddScoped<IEmailScheduler<InvitationEmailPayload>, EmailSchedulerService<InvitationEmailPayload>>();
        services.AddScoped<IEmailScheduler<InvitationAcceptedEmailPayload>, EmailSchedulerService<InvitationAcceptedEmailPayload>>();
        services.AddScoped<IEmailScheduler<InvitationRevokedEmailPayload>, EmailSchedulerService<InvitationRevokedEmailPayload>>();
        services.AddScoped<IEmailScheduler<PasswordRecoveryEmailPayload>, EmailSchedulerService<PasswordRecoveryEmailPayload>>();

        if (isTesting)
        {
            services.AddScoped<IPushBackgroundScheduler, NoOpPushBackgroundScheduler>();
        }
        else
        {
            services.AddScoped<IPushBackgroundScheduler, HangfirePushBackgroundScheduler>();
        }

        foreach (var contract in registry.Contracts)
        {
            foreach (var handlerType in contract.ExpectedHandlerTypes)
            {
                services.AddScoped(typeof(IBackgroundAction<>).MakeGenericType(contract.RuntimeType), handlerType);
            }
        }

        return services;
    }

    private static ServiceCollection CreateFullHostComposition(bool isTesting)
    {
        var services = new ServiceCollection();
        var configuration = CreateFullHostConfiguration(isTesting);

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddSingleton<IHostApplicationLifetime, TestHostApplicationLifetime>();
        services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);
        services.AddPlatformModule();
        services.AddIdentityModule();
        services.AddTrainingPlanningModule();
        services.AddNotificationsModule(configuration);
        services.AddReportingModule();
        services.AddApplication();
        services.AddInfrastructure(
            configuration,
            enableSensitiveLogging: false,
            isTesting,
            hostBackgroundServer: true);
        services.AddReportingApiAdapters();
        services.AddApplicationApiAdapters();
        services.AddNotificationsApiAdapters();
        services.AddSignalR();
        services.AddSingleton<IAccountSessionConnectionRegistry, AccountSessionConnectionRegistry>();
        services.AddScoped<IInAppNotificationPushPublisher, LgymApi.Api.Features.InAppNotification.SignalRNotificationPushPublisher>();
        services.AddBackgroundWorkerServices(isTesting);

        return services;
    }

    private static IConfiguration CreateFullHostConfiguration(bool isTesting)
    {
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values["Email:DeliveryMode"] = isTesting ? "Dummy" : "Smtp";
        values["Email:DummyOutputDirectory"] = "DummyOutbox";
        values["PhotoStorage:Provider"] = isTesting ? "Local" : "CloudflareR2";
        values["PhotoStorage:BucketName"] = "lgym-report-photos-test";
        values["PhotoStorage:Endpoint"] = "https://example.r2.cloudflarestorage.com";
        values["PhotoStorage:AccessKeyId"] = "test-access-key";
        values["PhotoStorage:SecretAccessKey"] = "test-secret-key";

        return TestConfigurationBuilder.BuildConfiguration(values);
    }

    private static void ValidateFullHostComposition(IServiceCollection services, bool isTesting)
    {
        ValidateSingleDescriptor(services, typeof(IAppConfigService), ServiceLifetime.Scoped, typeof(AppConfigService));
        ValidateSingleDescriptor(
            services,
            typeof(IAppConfigAuthorizationPort),
            ServiceLifetime.Scoped,
            typeof(AppConfigAuthorizationAdapter));
        ValidateSingleDescriptor(services, typeof(IEnumService), ServiceLifetime.Scoped, typeof(EnumService));
        ValidateSingleDescriptor(
            services,
            typeof(IUnitConverter<WeightUnits>),
            ServiceLifetime.Singleton,
            typeof(LinearUnitConverter<WeightUnits>));
        ValidateSingleDescriptor(
            services,
            typeof(IUnitConverter<HeightUnits>),
            ServiceLifetime.Singleton,
            typeof(LinearUnitConverter<HeightUnits>));
        ValidateSingleDescriptor(services, typeof(IAppConfigRepository), ServiceLifetime.Scoped, typeof(AppConfigRepository));
        ValidateSingleDescriptor(
            services,
            typeof(ICommittedIntentDispatcher),
            ServiceLifetime.Scoped,
            typeof(CommittedIntentDispatcher));
        ValidateSingleDescriptor(services, typeof(IUnitOfWork), ServiceLifetime.Scoped, typeof(EfUnitOfWork));
        ValidateSingleDescriptor(
            services,
            typeof(ICommandEnvelopeRepository),
            ServiceLifetime.Scoped,
            typeof(CommandEnvelopeRepository));
        ValidateSingleDescriptor(
            services,
            typeof(IApiIdempotencyRecordRepository),
            ServiceLifetime.Scoped,
            typeof(ApiIdempotencyRecordRepository));
        ValidateSingleDescriptor(
            services,
            typeof(IEmailNotificationsFeature),
            ServiceLifetime.Singleton,
            typeof(EmailNotificationsFeature));
        ValidateSingleDescriptor(services, typeof(IEmailMetrics), ServiceLifetime.Singleton, typeof(EmailMetrics));
        ValidateSingleDescriptor(
            services,
            typeof(IEmailTemplateComposerFactory),
            ServiceLifetime.Scoped,
            typeof(EmailTemplateComposerFactory));
        ValidateFactoryDescriptor(services, typeof(ApplicationMapper), ServiceLifetime.Singleton);
        ValidateFactoryDescriptor(services, typeof(IMapperRegistry), ServiceLifetime.Singleton);
        ValidateSingleDescriptor(
            services,
            typeof(IQueryPaginationService),
            ServiceLifetime.Scoped,
            typeof(QueryPaginationFacade));
        ValidateInstanceDescriptor(services, typeof(PaginationPolicy), ServiceLifetime.Singleton);
        ValidateInstanceDescriptor(services, typeof(EmailOptions), ServiceLifetime.Singleton);
        ValidateFactoryDescriptor(services, typeof(IEmailSender), ServiceLifetime.Scoped);
        ValidateSingleDescriptor(
            services,
            typeof(IPhotoStorageProvider),
            ServiceLifetime.Scoped,
            isTesting ? typeof(LocalPhotoStorageProvider) : typeof(CloudflareR2PhotoStorageProvider));
        ValidateSingleDescriptor(
            services,
            typeof(IReportPhotoPersistence),
            ServiceLifetime.Scoped,
            typeof(ReportPhotoPersistenceRepository));
        ValidateSingleDescriptor(services, typeof(IPushProviderSender), ServiceLifetime.Scoped, typeof(FcmPushSender));
        ValidateSingleDescriptor(services, typeof(ITokenService), ServiceLifetime.Scoped, typeof(TokenService));
        ValidateSingleDescriptor(
            services,
            typeof(IGoogleTokenValidator),
            ServiceLifetime.Scoped,
            typeof(GoogleTokenValidator));
        ValidateSingleDescriptor(
            services,
            typeof(ILegacyPasswordService),
            ServiceLifetime.Scoped,
            typeof(LegacyPasswordService));
        ValidateSingleDescriptor(services, typeof(IUserSessionStore), ServiceLifetime.Scoped, typeof(UserSessionStore));
        ValidateSingleDescriptor(
            services,
            typeof(IInAppNotificationPushPublisher),
            ServiceLifetime.Scoped,
            typeof(LgymApi.Api.Features.InAppNotification.SignalRNotificationPushPublisher));

        var expectedEmailSchedulerType = isTesting ? typeof(NoOpEmailBackgroundScheduler) : typeof(HangfireEmailBackgroundScheduler);
        var expectedActionSchedulerType = isTesting ? typeof(NoOpActionMessageScheduler) : typeof(HangfireActionMessageScheduler);
        var expectedPushSchedulerType = isTesting ? typeof(NoOpPushBackgroundScheduler) : typeof(HangfirePushBackgroundScheduler);
        ValidateSingleDescriptor(services, typeof(IEmailBackgroundScheduler), ServiceLifetime.Scoped, expectedEmailSchedulerType);
        ValidateSingleDescriptor(services, typeof(IActionMessageScheduler), ServiceLifetime.Scoped, expectedActionSchedulerType);
        ValidateSingleDescriptor(services, typeof(IPushBackgroundScheduler), ServiceLifetime.Scoped, expectedPushSchedulerType);

        ValidateImplementationCollection(services, typeof(IEmailTemplateComposer), ServiceLifetime.Scoped, expectedCount: 7);
        ValidateImplementationCollection(services, typeof(IMappingProfile), ServiceLifetime.Singleton, expectedCount: 49);
        Assert.That(services.Count(descriptor => descriptor.ServiceType == typeof(IAccountLookupService)), Is.EqualTo(1));
        Assert.That(services.Count(descriptor => descriptor.ServiceType == typeof(IAccountAccessReader)), Is.EqualTo(1));
        Assert.That(services.Count(descriptor => descriptor.ServiceType == typeof(IAccountSessionValidator)), Is.EqualTo(1));
        Assert.That(services.Count(descriptor => descriptor.ServiceType == typeof(IAuthenticatedAccountContextResolver)), Is.EqualTo(1));
        Assert.That((services.Where(descriptor => descriptor.ServiceType == typeof(IMappingProfile))).Where(descriptor => descriptor.ImplementationType == typeof(EnumLookupMappingProfile)), Has.Exactly(1).Items);
        Assert.That((services.Where(descriptor => descriptor.ServiceType == typeof(IMappingProfile))).Where(descriptor => descriptor.ImplementationType == typeof(PlanExerciseWorkoutAdapterMappingProfile)), Has.Exactly(1).Items);
        Assert.That((services.Where(descriptor => descriptor.ServiceType == typeof(IMappingProfile))).Where(descriptor => descriptor.ImplementationType == typeof(ManagedPlanCollaborationMappingProfile)), Has.Exactly(1).Items);
        Assert.That((services.Where(descriptor => descriptor.ServiceType == typeof(IMappingProfile))).Where(descriptor => descriptor.ImplementationType == typeof(IdentityApiAdapterMappingProfile)), Has.Exactly(1).Items);
        Assert.That((services.Where(descriptor => descriptor.ServiceType == typeof(IMappingProfile))).Where(descriptor => descriptor.ImplementationType == typeof(NutritionApiAdapterMappingProfile)), Has.Exactly(1).Items);
        Assert.That((services.Where(descriptor => descriptor.ServiceType == typeof(IMappingProfile))).Where(descriptor => descriptor.ImplementationType == typeof(PlanApiAdapterMappingProfile)), Has.Exactly(1).Items);

        var hangfireServerDescriptors = services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService)).ToArray();
        Assert.That(hangfireServerDescriptors, Has.Exactly(isTesting ? 0 : 1).Items);
        if (isTesting)
        {
            if (services.Any(descriptor => descriptor.ServiceType == typeof(JobStorage)))
            {
                throw new InvalidOperationException(
                    $"Testing composition must not register '{typeof(JobStorage).FullName}'.");
            }
        }
        else
        {
            ValidateFactoryDescriptor(services, typeof(JobStorage), ServiceLifetime.Singleton);
            ValidateFactoryDescriptor(services, typeof(IHostedService), ServiceLifetime.Transient);
        }
    }

    private static void ValidateCentralComposition(IServiceCollection services, Type expectedSchedulerType)
    {
        ValidateSingleDescriptor(services, typeof(CommandContractRegistry), ServiceLifetime.Singleton);
        ValidateSingleDescriptor(
            services,
            typeof(IInAppNotificationService),
            ServiceLifetime.Scoped,
            typeof(InAppNotificationService));
        ValidateSingleDescriptor(
            services,
            typeof(INotificationEventBridge),
            ServiceLifetime.Scoped,
            typeof(NotificationEventBridge));
        ValidateSingleDescriptor(services, typeof(ICommandDispatcher), ServiceLifetime.Scoped, typeof(CommandDispatcher));
        ValidateSingleDescriptor(
            services,
            typeof(IBackgroundActionResolver),
            ServiceLifetime.Scoped,
            typeof(BackgroundActionResolver));
        ValidateSingleDescriptor(
            services,
            typeof(IEmailScheduler<PasswordRecoveryEmailPayload>),
            ServiceLifetime.Scoped,
            typeof(EmailSchedulerService<PasswordRecoveryEmailPayload>));
        ValidateSingleDescriptor(
            services,
            typeof(IPasswordRecoveryEmailScheduler),
            ServiceLifetime.Scoped,
            typeof(PasswordRecoveryEmailSchedulerAdapter));
        ValidateSingleDescriptor(
            services,
            typeof(ICoachingEmailNotificationFeature),
            ServiceLifetime.Scoped,
            typeof(CoachingEmailNotificationSchedulerAdapter));
        ValidateSingleDescriptor(
            services,
            typeof(ICoachingEmailNotificationScheduler),
            ServiceLifetime.Scoped,
            typeof(CoachingEmailNotificationSchedulerAdapter));
        ValidateSingleDescriptor(
            services,
            typeof(IPushBackgroundScheduler),
            ServiceLifetime.Scoped,
            expectedSchedulerType);
        ValidateSingleDescriptor(
            services,
            typeof(IPushProviderSender),
            ServiceLifetime.Scoped,
            typeof(FcmPushSender));

        var registry = CommandContractRegistry.CreateDefault();
        Assert.That(registry.Contracts, Has.Exactly(19).Items);
        Assert.That(registry.Contracts.Sum(contract => contract.ExpectedHandlerTypes.Count), Is.EqualTo(20));
        BackgroundActionRegistrationValidator.Validate(services, registry);
    }

    private static void ValidateSingleDescriptor(
        IServiceCollection services,
        Type serviceType,
        ServiceLifetime expectedLifetime,
        Type? expectedImplementationType = null)
    {
        var descriptors = services.Where(descriptor => descriptor.ServiceType == serviceType).ToArray();
        if (descriptors.Length != 1)
        {
            throw new InvalidOperationException(
                $"Service '{serviceType.FullName}' must be registered exactly once; actual count is {descriptors.Length}.");
        }

        var descriptor = descriptors[0];
        if (descriptor.Lifetime != expectedLifetime)
        {
            throw new InvalidOperationException(
                $"Service '{serviceType.FullName}' must use lifetime '{expectedLifetime}'; actual lifetime is '{descriptor.Lifetime}'.");
        }

        if (expectedImplementationType != null && descriptor.ImplementationType != expectedImplementationType)
        {
            throw new InvalidOperationException(
                $"Service '{serviceType.FullName}' must use implementation '{expectedImplementationType.FullName}'; "
                + $"actual implementation is '{descriptor.ImplementationType?.FullName ?? "factory or instance"}'.");
        }
    }

    private static void ValidateFactoryDescriptor(
        IServiceCollection services,
        Type serviceType,
        ServiceLifetime expectedLifetime)
    {
        var descriptor = GetSingleDescriptor(services, serviceType);
        if (descriptor.Lifetime != expectedLifetime)
        {
            throw new InvalidOperationException(
                $"Service '{serviceType.FullName}' must use lifetime '{expectedLifetime}'; actual lifetime is '{descriptor.Lifetime}'.");
        }

        if (descriptor.ImplementationFactory == null
            || descriptor.ImplementationType != null
            || descriptor.ImplementationInstance != null)
        {
            var actualForm = descriptor.ImplementationInstance != null
                ? "instance"
                : descriptor.ImplementationType != null
                    ? "implementation"
                    : "empty";
            throw new InvalidOperationException(
                $"Service '{serviceType.FullName}' must use a factory descriptor; actual form is '{actualForm}'.");
        }
    }

    private static void ValidateInstanceDescriptor(
        IServiceCollection services,
        Type serviceType,
        ServiceLifetime expectedLifetime)
    {
        var descriptor = GetSingleDescriptor(services, serviceType);
        Assert.That(descriptor.Lifetime, Is.EqualTo(expectedLifetime));
        Assert.That(descriptor.ImplementationInstance, Is.Not.Null);
        Assert.That(descriptor.ImplementationType, Is.Null);
        Assert.That(descriptor.ImplementationFactory, Is.Null);
    }

    private static ServiceDescriptor GetSingleDescriptor(IServiceCollection services, Type serviceType)
    {
        var descriptors = services.Where(descriptor => descriptor.ServiceType == serviceType).ToArray();
        if (descriptors.Length != 1)
        {
            throw new InvalidOperationException(
                $"Service '{serviceType.FullName}' must be registered exactly once; actual count is {descriptors.Length}.");
        }

        return descriptors[0];
    }

    private static void ValidateImplementationCollection(
        IServiceCollection services,
        Type serviceType,
        ServiceLifetime expectedLifetime,
        int expectedCount)
    {
        var descriptors = services.Where(descriptor => descriptor.ServiceType == serviceType).ToArray();
        Assert.That(descriptors, Has.Exactly(expectedCount).Items);
        Assert.That((descriptors).All(descriptor => descriptor.Lifetime == expectedLifetime), Is.True);
        Assert.That((descriptors).All(descriptor => descriptor.ImplementationType != null), Is.True);
        Assert.That(descriptors.Select(descriptor => descriptor.ImplementationType), Is.Unique);
    }

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime, IDisposable
    {
        private readonly CancellationTokenSource _applicationStarted = new();
        private readonly CancellationTokenSource _applicationStopping = new();
        private readonly CancellationTokenSource _applicationStopped = new();

        public CancellationToken ApplicationStarted => _applicationStarted.Token;

        public CancellationToken ApplicationStopping => _applicationStopping.Token;

        public CancellationToken ApplicationStopped => _applicationStopped.Token;

        public void StopApplication()
        {
            _applicationStopping.Cancel();
            _applicationStopped.Cancel();
        }

        public void Dispose()
        {
            _applicationStarted.Dispose();
            _applicationStopping.Dispose();
            _applicationStopped.Dispose();
        }
    }
}
