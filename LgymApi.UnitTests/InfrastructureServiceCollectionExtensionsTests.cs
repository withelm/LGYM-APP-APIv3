using Hangfire;
using LgymApi.Application.Coaching;
using LgymApi.Application.Coaching.Contracts.BackgroundCommands;
using LgymApi.Application.Features.Reporting;
using LgymApi.Identity;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker;
using LgymApi.BackgroundWorker.Common.Jobs;
using LgymApi.Application.Options;
using LgymApi.Application.Pagination;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Contracts.Events;
using LgymApi.Application.Notifications.Contracts.Push;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Notifications.Providers.Fcm;
using LgymApi.Application.Notifications.Repositories;
using LgymApi.Application.Repositories;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Application.WorkoutProgress.ReportingIntegration;
using LgymApi.Application.Services;
using LgymApi.Infrastructure;
using LgymApi.Platform;
using LgymApi.Infrastructure.Options;
using LgymApi.Infrastructure.Pagination;
using LgymApi.Infrastructure.Repositories;
using LgymApi.Infrastructure.Repositories.Reporting;
using LgymApi.Infrastructure.Repositories.ReferenceData;
using LgymApi.Infrastructure.Services;
using LgymApi.Infrastructure.UnitOfWork;
using LgymApi.BackgroundWorker.Actions;
using LgymApi.BackgroundWorker.Actions.Contracts;
using LgymApi.BackgroundWorker.Notifications;
using LgymApi.Notifications;
using LgymApi.Application.Abstractions.Storage;
using LgymApi.TestUtils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ApplicationCommandDispatcher = LgymApi.Application.Platform.Contracts.BackgroundCommands.ICommandDispatcher;
using IActionMessageScheduler = LgymApi.BackgroundWorker.Common.IActionMessageScheduler;
using IEmailBackgroundScheduler = LgymApi.BackgroundWorker.Common.IEmailBackgroundScheduler;
using QueryPaginationFacade = LgymApi.Infrastructure.Pagination.QueryPaginationService;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class InfrastructureServiceCollectionExtensionsTests
{
    [Test]
    public void AddInfrastructure_RegistersNoOpScheduler_WhenTesting()
    {
        var configuration = TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test",
            ["PhotoStorage:Provider"] = "CloudflareR2",
            ["PhotoStorage:BucketName"] = "lgym-report-photos-dev",
            ["PhotoStorage:Endpoint"] = "https://38c1c25f99af223efee28a9afcf5d575.r2.cloudflarestorage.com",
            ["PhotoStorage:AccessKeyId"] = "test-access-key",
            ["PhotoStorage:SecretAccessKey"] = "test-secret-key"
        });

        using var provider = TestServiceProviderFactory.CreateServiceProvider(
            CompositionRootTestHost.CreateFactoryComposition(configuration));
        var scheduler = provider.GetRequiredService<IEmailBackgroundScheduler>();
        Assert.That(scheduler, Is.TypeOf<NoOpEmailBackgroundScheduler>());
    }

    [Test]
    public void AddNotificationsModule_UsesSmtpDeliveryModeByDefault()
    {
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values.Remove("Email:DeliveryMode");
        var configuration = TestConfigurationBuilder.BuildConfiguration(values);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotificationsModule(configuration);

        AssertFactoryDescriptor<IEmailSender>(services, ServiceLifetime.Scoped);
        AssertTypeDescriptor<SmtpEmailSender, SmtpEmailSender>(services, ServiceLifetime.Scoped);
        AssertTypeDescriptor<DummyEmailSender, DummyEmailSender>(services, ServiceLifetime.Scoped);

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<IEmailSender>();
        Assert.That(sender, Is.TypeOf<SmtpEmailSender>());
    }

    [Test]
    public void AddNotificationsModule_UsesDummyEmailSender_WhenModeIsDummy()
    {
        var services = new ServiceCollection();
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values["Email:DeliveryMode"] = "Dummy";
        values["Email:DummyOutputDirectory"] = "DummyOutbox";
        values["Email:SmtpHost"] = string.Empty;
        var configuration = TestConfigurationBuilder.BuildConfiguration(values);

        services.AddNotificationsModule(configuration);

        AssertFactoryDescriptor<IEmailSender>(services, ServiceLifetime.Scoped);
        AssertTypeDescriptor<SmtpEmailSender, SmtpEmailSender>(services, ServiceLifetime.Scoped);
        AssertTypeDescriptor<DummyEmailSender, DummyEmailSender>(services, ServiceLifetime.Scoped);

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<IEmailSender>();
        Assert.That(sender, Is.TypeOf<DummyEmailSender>());
    }

    [TestCase(true, "Local", typeof(LocalPhotoStorageProvider))]
    [TestCase(false, "CloudflareR2", typeof(CloudflareR2PhotoStorageProvider))]
    public void AddInfrastructure_RegistersAndResolvesExactPlatformAdapters(
        bool isTesting,
        string photoStorageProvider,
        Type expectedPhotoStorageProviderType)
    {
        var services = new ServiceCollection();
        var configuration = CreateInfrastructureConfiguration(photoStorageProvider);

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddPlatformModule();
        services.AddIdentityModule();
        services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting);
        services.AddNotificationsModule(configuration);

        AssertTypeDescriptor<IAppConfigRepository, AppConfigRepository>(services, ServiceLifetime.Scoped);
        AssertTypeDescriptor<ICommittedIntentDispatcher, CommittedIntentDispatcher>(services, ServiceLifetime.Scoped);
        AssertTypeDescriptor<IUnitOfWork, EfUnitOfWork>(services, ServiceLifetime.Scoped);
        Assert.That((services).Where(descriptor =>
            descriptor.ServiceType == typeof(IActorRowSecurityScopeFactory)
            && descriptor.ImplementationType != null
            && descriptor.ImplementationType.FullName == "LgymApi.Infrastructure.RowSecurity.EfActorRowSecurityScopeFactory"
            && descriptor.Lifetime == ServiceLifetime.Scoped), Has.Exactly(1).Items);
        AssertTypeDescriptor<ICommandEnvelopeRepository, CommandEnvelopeRepository>(services, ServiceLifetime.Scoped);
        AssertTypeDescriptor<IApiIdempotencyRecordRepository, ApiIdempotencyRecordRepository>(services, ServiceLifetime.Scoped);
        AssertTypeDescriptor<IEmailNotificationsFeature, EmailNotificationsFeature>(services, ServiceLifetime.Singleton);
        AssertTypeDescriptor<IEmailMetrics, EmailMetrics>(services, ServiceLifetime.Singleton);
        AssertTypeDescriptor<IEmailTemplateComposerFactory, EmailTemplateComposerFactory>(services, ServiceLifetime.Scoped);
        AssertFactoryDescriptor<IMapperRegistry>(services, ServiceLifetime.Singleton);
        AssertTypeDescriptor<IQueryPaginationService, QueryPaginationFacade>(services, ServiceLifetime.Scoped);
        AssertInstanceDescriptor<PaginationPolicy>(services, ServiceLifetime.Singleton);
        AssertInstanceDescriptor<EmailOptions>(services, ServiceLifetime.Singleton);
        AssertInstanceDescriptor<PhotoStorageOptions>(services, ServiceLifetime.Singleton);
        AssertTypeDescriptor<LocalPhotoDevelopmentStore, LocalPhotoDevelopmentStore>(services, ServiceLifetime.Singleton);
        AssertTypeDescriptor<IReportTemplatePersistence, ReportTemplatePersistenceRepository>(services, ServiceLifetime.Scoped);
        AssertTypeDescriptor<IReportRequestSubmissionPersistence, ReportRequestSubmissionPersistenceRepository>(services, ServiceLifetime.Scoped);
        AssertTypeDescriptor<IRecurringReportAssignmentPersistence, RecurringReportAssignmentPersistenceRepository>(services, ServiceLifetime.Scoped);
        AssertTypeDescriptor<IReportPhotoPersistence, ReportPhotoPersistenceRepository>(services, ServiceLifetime.Scoped);
        AssertTypeDescriptor<IReportingRelationshipAccessPersistence, ReportingRelationshipAccessPersistenceRepository>(services, ServiceLifetime.Scoped);
        AssertTypeDescriptor<IReportSubmissionAcceptedProgressPersistence, ReportSubmissionAcceptedProgressPersistenceRepository>(services, ServiceLifetime.Scoped);
        AssertTypeDescriptor(services, typeof(IPhotoStorageProvider), expectedPhotoStorageProviderType, ServiceLifetime.Scoped);
        AssertTypeDescriptor<IPushProviderSender, FcmPushSender>(services, ServiceLifetime.Scoped);
        AssertTypeDescriptor<ITokenService, TokenService>(services, ServiceLifetime.Scoped);
        AssertTypeDescriptor<IGoogleTokenValidator, GoogleTokenValidator>(services, ServiceLifetime.Scoped);
        AssertTypeDescriptor<ILegacyPasswordService, LegacyPasswordService>(services, ServiceLifetime.Scoped);
        AssertTypeDescriptor<IUserSessionStore, UserSessionStore>(services, ServiceLifetime.Scoped);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var scopedServices = scope.ServiceProvider;

        Assert.That(scopedServices.GetRequiredService<IAppConfigRepository>(), Is.TypeOf<AppConfigRepository>());
        Assert.That(scopedServices.GetRequiredService<ICommittedIntentDispatcher>(), Is.TypeOf<CommittedIntentDispatcher>());
        Assert.That(scopedServices.GetRequiredService<IUnitOfWork>(), Is.TypeOf<EfUnitOfWork>());
        Assert.That(scopedServices.GetRequiredService<IActorRowSecurityScopeFactory>().GetType().Name, Is.EqualTo("EfActorRowSecurityScopeFactory"));
        Assert.That(scopedServices.GetRequiredService<ICommandEnvelopeRepository>(), Is.TypeOf<CommandEnvelopeRepository>());
        Assert.That(scopedServices.GetRequiredService<IApiIdempotencyRecordRepository>(), Is.TypeOf<ApiIdempotencyRecordRepository>());
        Assert.That(provider.GetRequiredService<IEmailNotificationsFeature>(), Is.TypeOf<EmailNotificationsFeature>());
        Assert.That(provider.GetRequiredService<IEmailMetrics>(), Is.TypeOf<EmailMetrics>());
        Assert.That(scopedServices.GetRequiredService<IEmailTemplateComposerFactory>(), Is.TypeOf<EmailTemplateComposerFactory>());
        Assert.That(provider.GetRequiredService<IMapperRegistry>(), Is.TypeOf<MapperRegistry>());
        Assert.That(scopedServices.GetRequiredService<IQueryPaginationService>(), Is.TypeOf<QueryPaginationFacade>());
        Assert.That(provider.GetRequiredService<LocalPhotoDevelopmentStore>(), Is.Not.Null);
        Assert.That(scopedServices.GetRequiredService<IReportTemplatePersistence>(), Is.TypeOf<ReportTemplatePersistenceRepository>());
        Assert.That(scopedServices.GetRequiredService<IReportRequestSubmissionPersistence>(), Is.TypeOf<ReportRequestSubmissionPersistenceRepository>());
        Assert.That(scopedServices.GetRequiredService<IRecurringReportAssignmentPersistence>(), Is.TypeOf<RecurringReportAssignmentPersistenceRepository>());
        Assert.That(scopedServices.GetRequiredService<IReportPhotoPersistence>(), Is.TypeOf<ReportPhotoPersistenceRepository>());
        Assert.That(scopedServices.GetRequiredService<IReportingRelationshipAccessPersistence>(), Is.TypeOf<ReportingRelationshipAccessPersistenceRepository>());
        Assert.That(scopedServices.GetRequiredService<IPhotoStorageProvider>(), Is.TypeOf(expectedPhotoStorageProviderType));
        Assert.That(scopedServices.GetRequiredService<IPushProviderSender>(), Is.TypeOf<FcmPushSender>());
        Assert.That(scopedServices.GetRequiredService<ITokenService>(), Is.TypeOf<TokenService>());
        Assert.That(scopedServices.GetRequiredService<IGoogleTokenValidator>(), Is.TypeOf<GoogleTokenValidator>());
        Assert.That(scopedServices.GetRequiredService<ILegacyPasswordService>(), Is.TypeOf<LegacyPasswordService>());
        Assert.That(scopedServices.GetRequiredService<IUserSessionStore>(), Is.TypeOf<UserSessionStore>());
    }

    [Test]
    public void AddNotificationsModule_Throws_WhenDeliveryModeInvalid()
    {
        var services = new ServiceCollection();
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values["Email:DeliveryMode"] = "something-else";

        var action = () =>
            services.AddNotificationsModule(TestConfigurationBuilder.BuildConfiguration(values));

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("Email:DeliveryMode must be one of: Smtp, Dummy.").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
        Assert.That(services, Is.Empty);
    }

    [TestCase("Unsupported", "Unsupported photo storage provider: Unsupported")]
    [TestCase("CloudflareR2", "PhotoStorage:BucketName is required for CloudflareR2.")]
    public void AddInfrastructure_RejectsInvalidPhotoProviderDuringRegistration(
        string providerName,
        string expectedMessage)
    {
        var services = new ServiceCollection();
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values["PhotoStorage:Provider"] = providerName;
        if (providerName == "CloudflareR2")
        {
            values["PhotoStorage:BucketName"] = "";
            values["PhotoStorage:Endpoint"] = "https://example.r2.cloudflarestorage.com";
            values["PhotoStorage:AccessKeyId"] = "test-access-key";
            values["PhotoStorage:SecretAccessKey"] = "test-secret-key";
        }

        services.AddPlatformModule();
        var action = () => services.AddInfrastructure(
            TestConfigurationBuilder.BuildConfiguration(values),
            enableSensitiveLogging: false,
            isTesting: true);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape(expectedMessage).Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
        Assert.That((services).Where(descriptor => descriptor.ServiceType == typeof(IAppConfigRepository)), Has.Exactly(1).Items);
        Assert.That((services).Any(descriptor => descriptor.ServiceType == typeof(IPhotoStorageProvider)), Is.False);
        Assert.That((services).Any(descriptor => descriptor.ServiceType == typeof(IPushProviderSender)), Is.False);
    }

    [Test]
    public void AddNotificationsModule_Throws_WhenDummyOutputDirectoryMissingInDummyMode()
    {
        var services = new ServiceCollection();
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values["Email:DeliveryMode"] = "Dummy";
        values["Email:DummyOutputDirectory"] = "   ";

        var action = () =>
            services.AddNotificationsModule(TestConfigurationBuilder.BuildConfiguration(values));

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("Email:DummyOutputDirectory is required when Email:DeliveryMode is Dummy.").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [TestCase(null, "Email:InvitationBaseUrl is required.")]
    [TestCase("not-an-url", "Email:InvitationBaseUrl must be a valid absolute URL.")]
    public void AddNotificationsModule_Throws_ForInvalidInvitationBaseUrl(string? invitationBaseUrl, string expectedMessage)
    {
        var services = new ServiceCollection();
        var configuration = TestConfigurationBuilder.BuildEnabledEmailConfiguration();
        var values = new Dictionary<string, string?>(configuration.AsEnumerable().ToDictionary(k => k.Key, v => v.Value))
        {
            ["Email:InvitationBaseUrl"] = invitationBaseUrl
        };

        var action = () =>
            services.AddNotificationsModule(TestConfigurationBuilder.BuildConfiguration(values));

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape(expectedMessage).Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void AddNotificationsModule_Throws_WhenTemplateRootPathMissing()
    {
        var services = new ServiceCollection();
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values["Email:TemplateRootPath"] = "";

        var action = () =>
            services.AddNotificationsModule(TestConfigurationBuilder.BuildConfiguration(values));

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("Email:TemplateRootPath is required when email is enabled.").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void AddNotificationsModule_Throws_WhenFromAddressInvalid()
    {
        var services = new ServiceCollection();
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values["Email:FromAddress"] = "invalid-email";

        var action = () =>
            services.AddNotificationsModule(TestConfigurationBuilder.BuildConfiguration(values));

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("Email:FromAddress must be a valid email address.").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void AddNotificationsModule_Throws_WhenSmtpHostMissing()
    {
        var services = new ServiceCollection();
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values["Email:SmtpHost"] = "";

        var action = () =>
            services.AddNotificationsModule(TestConfigurationBuilder.BuildConfiguration(values));

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("Email:SmtpHost is required when email is enabled.").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void AddNotificationsModule_Throws_WhenSmtpPortNonPositive()
    {
        var services = new ServiceCollection();
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values["Email:SmtpPort"] = "0";

        var action = () =>
            services.AddNotificationsModule(TestConfigurationBuilder.BuildConfiguration(values));

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("Email:SmtpPort must be greater than 0 when email is enabled.").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void AddBackgroundWorkerServices_RegistersCommandDispatcher()
    {
        var configuration = TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test",
            ["PhotoStorage:Provider"] = "CloudflareR2",
            ["PhotoStorage:BucketName"] = "lgym-report-photos-dev",
            ["PhotoStorage:Endpoint"] = "https://38c1c25f99af223efee28a9afcf5d575.r2.cloudflarestorage.com",
            ["PhotoStorage:AccessKeyId"] = "test-access-key",
            ["PhotoStorage:SecretAccessKey"] = "test-secret-key"
        });

        using var provider = TestServiceProviderFactory.CreateServiceProvider(
            CompositionRootTestHost.CreateFactoryComposition(configuration));
        var dispatcher = provider.GetRequiredService<ApplicationCommandDispatcher>();
        Assert.That(dispatcher, Is.TypeOf<CommandDispatcher>());
    }

    [Test]
    public void AddBackgroundWorkerServices_RegistersNoOpSchedulersAndRetainsApplicationBridge_WhenTesting()
    {
        var services = new ServiceCollection();
        var configuration = TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test"
        });

        services.AddLogging();
        services.AddNotificationsModule(configuration);
        services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: true);
        services.AddBackgroundWorkerServices(isTesting: true);
        services.AddScoped<IInAppNotificationPushPublisher, FakeInAppNotificationPushPublisher>();

        using var provider = services.BuildServiceProvider();
        var scheduler = provider.GetRequiredService<IActionMessageScheduler>();
        Assert.That(scheduler, Is.TypeOf<NoOpActionMessageScheduler>());
        Assert.That(provider.GetRequiredService<IPushBackgroundScheduler>(), Is.TypeOf<LgymApi.BackgroundWorker.Services.NoOpPushBackgroundScheduler>());
        Assert.That(provider.GetRequiredService<INotificationEventBridge>(), Is.TypeOf<NotificationEventBridge>());
    }

    [Test]
    public void AddBackgroundWorkerServices_DoesNotDuplicateNotificationInfrastructureRegistrations()
    {
        var services = new ServiceCollection();
        var configuration = TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test",
            ["PhotoStorage:Provider"] = "CloudflareR2",
            ["PhotoStorage:BucketName"] = "lgym-report-photos-dev",
            ["PhotoStorage:Endpoint"] = "https://38c1c25f99af223efee28a9afcf5d575.r2.cloudflarestorage.com",
            ["PhotoStorage:AccessKeyId"] = "test-access-key",
            ["PhotoStorage:SecretAccessKey"] = "test-secret-key"
        });

        services.AddLogging();
        services.AddNotificationsModule(configuration);
        services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: true);
        services.AddBackgroundWorkerServices(isTesting: true);
        services.AddScoped<IInAppNotificationPushPublisher, FakeInAppNotificationPushPublisher>();

        Assert.That(services.Count(descriptor => descriptor.ServiceType == typeof(IPushInstallationRepository)), Is.EqualTo(1));
        Assert.That(services.Count(descriptor => descriptor.ServiceType == typeof(IPushNotificationMessageRepository)), Is.EqualTo(1));
        Assert.That(services.Count(descriptor => descriptor.ServiceType == typeof(IInAppNotificationRepository)), Is.EqualTo(1));
        Assert.That(services.Count(descriptor => descriptor.ServiceType == typeof(IPushBackgroundScheduler)), Is.EqualTo(1));
        Assert.That(services.Count(descriptor => descriptor.ServiceType == typeof(IPushProviderSender)), Is.EqualTo(1));
    }

    [Test]
    public void AddNotificationsModule_RegistersApplicationAndInfrastructureWithoutWorkerRuntimeDescriptors()
    {
        var services = new ServiceCollection();
        var configuration = TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test",
            ["PhotoStorage:Provider"] = "CloudflareR2",
            ["PhotoStorage:BucketName"] = "lgym-report-photos-dev",
            ["PhotoStorage:Endpoint"] = "https://38c1c25f99af223efee28a9afcf5d575.r2.cloudflarestorage.com",
            ["PhotoStorage:AccessKeyId"] = "test-access-key",
            ["PhotoStorage:SecretAccessKey"] = "test-secret-key"
        });

        services.AddNotificationsInfrastructure();
        services.AddNotificationsModule(configuration);

        Assert.That(services.Count(descriptor => descriptor.ServiceType == typeof(IInAppNotificationService)), Is.EqualTo(1));
        Assert.That(services.Count(descriptor => descriptor.ServiceType == typeof(INotificationEventBridge)), Is.EqualTo(1));
        Assert.That(services.Count(descriptor => descriptor.ServiceType == typeof(IPushInstallationRepository)), Is.EqualTo(1));
        Assert.That(services.Count(descriptor => descriptor.ServiceType == typeof(IPushNotificationMessageRepository)), Is.EqualTo(1));
        Assert.That(services.Count(descriptor => descriptor.ServiceType == typeof(IInAppNotificationRepository)), Is.EqualTo(1));
        Assert.That(services.Count(descriptor => descriptor.ServiceType == typeof(IEmailBackgroundScheduler)), Is.EqualTo(0));
        Assert.That(services.Count(descriptor => descriptor.ServiceType == typeof(IActionMessageScheduler)), Is.EqualTo(0));
        Assert.That(services.Count(descriptor => descriptor.ServiceType == typeof(IPushBackgroundScheduler)), Is.EqualTo(0));
        Assert.That(services.Count(descriptor => descriptor.ServiceType == typeof(JobStorage)), Is.EqualTo(0));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void FullHostComposition_RetainsApplicationNotificationBridge(bool isTesting)
    {
        var services = new ServiceCollection();
        var configuration = TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test"
        });

        services.AddNotificationsInfrastructure();
        services.AddNotificationsModule(configuration);
        services.AddBackgroundWorkerServices(isTesting);

        Assert.That(services.Count(descriptor => descriptor.ServiceType == typeof(IInAppNotificationService)), Is.EqualTo(1));
        var assertedValue113 = (services
                    .Where(descriptor => descriptor.ServiceType == typeof(INotificationEventBridge))).ToArray();
        Assert.That(assertedValue113, Has.Exactly(1).Items);
        var bridge = assertedValue113.Single();
        Assert.That(bridge.ImplementationType, Is.EqualTo(typeof(NotificationEventBridge)));
    }

    [TestCase(true, typeof(NoOpEmailBackgroundScheduler))]
    [TestCase(false, typeof(HangfireEmailBackgroundScheduler))]
    public void FullHostComposition_ResolvesCoachingEmailPortsExactlyOnce(
        bool isTesting,
        Type expectedBackgroundScheduler)
    {
        var services = new ServiceCollection();
        var configuration = TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test",
            ["PhotoStorage:Provider"] = "CloudflareR2",
            ["PhotoStorage:BucketName"] = "lgym-report-photos-dev",
            ["PhotoStorage:Endpoint"] = "https://38c1c25f99af223efee28a9afcf5d575.r2.cloudflarestorage.com",
            ["PhotoStorage:AccessKeyId"] = "test-access-key",
            ["PhotoStorage:SecretAccessKey"] = "test-secret-key"
        });

        services.AddLogging();
        services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting);
        services.AddNotificationsModule(configuration);
        services.AddBackgroundWorkerServices(isTesting);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var scopedServices = scope.ServiceProvider;

        var assertedValue114 = (scopedServices.GetServices<ICoachingEmailNotificationFeature>()).ToArray();
        Assert.That(assertedValue114, Has.Exactly(1).Items);
        Assert.That(assertedValue114.Single(), Is.TypeOf<CoachingEmailNotificationSchedulerAdapter>());
        var assertedValue115 = (scopedServices.GetServices<ICoachingEmailNotificationScheduler>()).ToArray();
        Assert.That(assertedValue115, Has.Exactly(1).Items);
        Assert.That(assertedValue115.Single(), Is.TypeOf<CoachingEmailNotificationSchedulerAdapter>());
        Assert.That(scopedServices.GetRequiredService<IEmailBackgroundScheduler>(), Is.TypeOf(expectedBackgroundScheduler));
    }

    [TestCaseSource(nameof(FullHostPushCompositionManifest))]
    public void FullHostPushComposition_CharacterizesCurrentDescriptorsAndKeepsFutureOwnershipManifest(PushCompositionManifest expectation)
    {
        var services = new ServiceCollection();
        var configuration = TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test"
        });

        services.AddNotificationsModule(configuration);
        services.AddBackgroundWorkerServices(expectation.IsTesting);

        var assertedValue116 = (services
                    .Where(descriptor => descriptor.ServiceType == typeof(IPushBackgroundScheduler))).ToArray();
        Assert.That(assertedValue116, Has.Exactly(1).Items);
        var schedulerDescriptor = assertedValue116.Single();
        var assertedValue117 = (services
                    .Where(descriptor => descriptor.ServiceType == typeof(IPushProviderSender))).ToArray();
        Assert.That(assertedValue117, Has.Exactly(1).Items);
        var providerDescriptor = assertedValue117.Single();

        Assert.That(schedulerDescriptor.Lifetime, Is.EqualTo(ServiceLifetime.Scoped));
        Assert.That(schedulerDescriptor.ImplementationType, Is.EqualTo(expectation.CurrentSchedulerImplementation));
        Assert.That(providerDescriptor.Lifetime, Is.EqualTo(ServiceLifetime.Scoped));
        Assert.That(providerDescriptor.ImplementationType, Is.EqualTo(expectation.CurrentProviderImplementation));
        Assert.That(expectation.FutureSchedulerSelector, Is.EqualTo("Worker"));
        Assert.That(expectation.FutureProviderImplementationOwner, Is.EqualTo("Notifications"));
    }

    [TestCase(
        true,
        typeof(NoOpEmailBackgroundScheduler),
        typeof(NoOpActionMessageScheduler),
        typeof(LgymApi.BackgroundWorker.Services.NoOpPushBackgroundScheduler))]
    [TestCase(
        false,
        typeof(HangfireEmailBackgroundScheduler),
        typeof(HangfireActionMessageScheduler),
        typeof(LgymApi.BackgroundWorker.Services.HangfirePushBackgroundScheduler))]
    public void AddBackgroundWorkerServices_RegistersAndResolvesExactEnvironmentSchedulers(
        bool isTesting,
        Type expectedEmailSchedulerType,
        Type expectedActionSchedulerType,
        Type expectedPushSchedulerType)
    {
        var services = new ServiceCollection();
        var configuration = CreateInfrastructureConfiguration(isTesting ? "Local" : "CloudflareR2");

        services.AddLogging();
        services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting);
        services.AddBackgroundWorkerServices(isTesting);

        AssertTypeDescriptor(services, typeof(IEmailBackgroundScheduler), expectedEmailSchedulerType, ServiceLifetime.Scoped);
        AssertTypeDescriptor(services, typeof(IActionMessageScheduler), expectedActionSchedulerType, ServiceLifetime.Scoped);
        AssertTypeDescriptor(services, typeof(IPushBackgroundScheduler), expectedPushSchedulerType, ServiceLifetime.Scoped);
        if (isTesting)
        {
            Assert.That((services).Any(descriptor => descriptor.ServiceType == typeof(JobStorage)), Is.False);
        }
        else
        {
            AssertFactoryDescriptor<JobStorage>(services, ServiceLifetime.Singleton);
        }

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var scopedServices = scope.ServiceProvider;

        Assert.That(scopedServices.GetRequiredService<IEmailBackgroundScheduler>(), Is.TypeOf(expectedEmailSchedulerType));
        Assert.That(scopedServices.GetRequiredService<IActionMessageScheduler>(), Is.TypeOf(expectedActionSchedulerType));
        Assert.That(scopedServices.GetRequiredService<IPushBackgroundScheduler>(), Is.TypeOf(expectedPushSchedulerType));

        if (isTesting)
        {
            Assert.That(provider.GetService<JobStorage>(), Is.Null);
        }
        else
        {
            Assert.That(provider.GetRequiredService<JobStorage>().GetType().FullName, Is.EqualTo("Hangfire.PostgreSql.PostgreSqlStorage"));
        }
    }

    [Test]
    public void AddNotificationsModule_Throws_WhenPushSendsEnabledWithoutProjectId()
    {
        var services = new ServiceCollection();
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values["PushNotifications:SendEnabled"] = "true";
        values["PushNotifications:Fcm:CredentialsJson"] = "{ }";

        var action = () => services.AddNotificationsModule(TestConfigurationBuilder.BuildConfiguration(values));

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("PushNotifications:Fcm:ProjectId is required when push notifications are enabled.").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [TestCase(
        "PushNotifications:Fcm:CredentialsJson",
        "PushNotifications:Fcm:CredentialsPath or PushNotifications:Fcm:CredentialsJson is required when push notifications are enabled.")]
    [TestCase(
        "PushNotifications:Fcm:BaseUrl",
        "PushNotifications:Fcm:BaseUrl is required when push notifications are enabled.")]
    public void AddNotificationsModule_RejectsMissingEnabledFcmConfiguration(string missingSetting, string expectedMessage)
    {
        var services = new ServiceCollection();
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values["PushNotifications:SendEnabled"] = "true";
        values["PushNotifications:Fcm:ProjectId"] = "lgym-test";
        values["PushNotifications:Fcm:CredentialsJson"] = "{ }";
        values["PushNotifications:Fcm:BaseUrl"] = "https://fcm.example.test";
        values[missingSetting] = string.Empty;

        var action = () => services.AddNotificationsModule(TestConfigurationBuilder.BuildConfiguration(values));

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape(expectedMessage).Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void AddNotificationsModule_DoesNotRequireFcmCredentials_WhenPushSendsDisabled()
    {
        var services = new ServiceCollection();
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values["PushNotifications:SendEnabled"] = "false";
        values["PushNotifications:StaleTokenCleanupEnabled"] = "true";

        var action = () => services.AddNotificationsModule(TestConfigurationBuilder.BuildConfiguration(values));

        Assert.That(() => { action(); }, Throws.Nothing);
    }

    [Test]
    public void AddBackgroundWorkerServices_RegistersHangfireScheduler_WhenNotTesting()
    {
        var services = new ServiceCollection();
        var configuration = TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test",
            ["PhotoStorage:Provider"] = "CloudflareR2",
            ["PhotoStorage:BucketName"] = "lgym-report-photos-dev",
            ["PhotoStorage:Endpoint"] = "https://38c1c25f99af223efee28a9afcf5d575.r2.cloudflarestorage.com",
            ["PhotoStorage:AccessKeyId"] = "test-access-key",
            ["PhotoStorage:SecretAccessKey"] = "test-secret-key"
        });

        services.AddLogging();
        services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: false);
        services.AddBackgroundWorkerServices(isTesting: false);

        using var provider = services.BuildServiceProvider();
        var scheduler = provider.GetRequiredService<IActionMessageScheduler>();
        Assert.That(scheduler, Is.TypeOf<HangfireActionMessageScheduler>());
    }

    [Test]
    public void AddBackgroundWorkerServices_RegistersOrchestratorService()
    {
        var configuration = TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test"
        });

        using var provider = TestServiceProviderFactory.CreateServiceProvider(
            CompositionRootTestHost.CreateFactoryComposition(configuration));
        var orchestrator = provider.GetRequiredService<BackgroundActionOrchestratorService>();
        Assert.That(orchestrator, Is.Not.Null);
    }

    [Test]
    public void AddBackgroundWorkerServices_RegistersCommittedIntentDispatchJob()
    {
        var configuration = TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test",
            ["PhotoStorage:Provider"] = "CloudflareR2",
            ["PhotoStorage:BucketName"] = "lgym-report-photos-dev",
            ["PhotoStorage:Endpoint"] = "https://38c1c25f99af223efee28a9afcf5d575.r2.cloudflarestorage.com",
            ["PhotoStorage:AccessKeyId"] = "test-access-key",
            ["PhotoStorage:SecretAccessKey"] = "test-secret-key"
        });

        using var provider = TestServiceProviderFactory.CreateServiceProvider(
            CompositionRootTestHost.CreateFactoryComposition(configuration));

        var job = provider.GetRequiredService<ICommittedIntentDispatchJob>();
        Assert.That(job, Is.TypeOf<LgymApi.BackgroundWorker.Jobs.CommittedIntentDispatchJob>());
    }

    [Test]
    public void AddBackgroundWorkerServices_RegistersInvitationInAppNotificationHandlers()
    {
        var services = new ServiceCollection();
        var configuration = TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test",
            ["PhotoStorage:Provider"] = "CloudflareR2",
            ["PhotoStorage:BucketName"] = "lgym-report-photos-dev",
            ["PhotoStorage:Endpoint"] = "https://38c1c25f99af223efee28a9afcf5d575.r2.cloudflarestorage.com",
            ["PhotoStorage:AccessKeyId"] = "test-access-key",
            ["PhotoStorage:SecretAccessKey"] = "test-secret-key"
        });

        services.AddLogging();
        services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);
        services.AddPlatformModule();
        services.AddIdentityModule();
        services.AddCoachingModule();
        services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: true);
        services.AddNotificationsModule(configuration);
        services.AddBackgroundWorkerServices(isTesting: true);
        services.AddScoped<IInAppNotificationPushPublisher, FakeInAppNotificationPushPublisher>();

        using var provider = services.BuildServiceProvider();

        Assert.That(provider.GetRequiredService<IBackgroundAction<TrainerInvitationCreatedInAppNotificationCommand>>(), Is.TypeOf<TrainerInvitationCreatedInAppNotificationCommandHandler>());
        Assert.That(provider.GetRequiredService<IBackgroundAction<TrainerInvitationAcceptedInAppNotificationCommand>>(), Is.TypeOf<TrainerInvitationAcceptedInAppNotificationCommandHandler>());
        Assert.That(provider.GetRequiredService<IBackgroundAction<TrainerInvitationRejectedInAppNotificationCommand>>(), Is.TypeOf<TrainerInvitationRejectedInAppNotificationCommandHandler>());
    }

    [Test]
    public void AddInfrastructure_RegistersConfiguredAppDefaults()
    {
        var services = new ServiceCollection();
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values["AppDefaults:PreferredLanguage"] = "pl-PL";
        values["AppDefaults:PreferredTimeZone"] = "UTC";
        var configuration = TestConfigurationBuilder.BuildConfiguration(values);

        services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: true);

        using var provider = services.BuildServiceProvider();
        var defaults = provider.GetRequiredService<AppDefaultsOptions>();

        Assert.That(defaults.PreferredLanguage, Is.EqualTo("pl-PL"));
        Assert.That(defaults.PreferredTimeZone, Is.EqualTo("UTC"));
    }

    [Test]
    public void AddInfrastructure_UsesLocalPhotoStorageProvider_WhenTestingAndProviderLocal()
    {
        var services = new ServiceCollection();
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values["PhotoStorage:Provider"] = "Local";
        var configuration = TestConfigurationBuilder.BuildConfiguration(values);

        services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: true);

        using var provider = services.BuildServiceProvider();
        Assert.That(provider.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>(), Is.Not.Null);
        Assert.That(provider.GetRequiredService<IPhotoStorageProvider>(), Is.TypeOf<LocalPhotoStorageProvider>());
    }

    [TestCase("")]
    [TestCase("short-local-photo-key")]
    public void AddInfrastructure_Throws_WhenDevelopmentLocalSigningKeyIsMissingOrTooShort(string signingKey)
    {
        var services = new ServiceCollection();
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values["PhotoStorage:Provider"] = "Local";
        values["PhotoStorage:LocalDevelopmentSigningKey"] = signingKey;
        var configuration = TestConfigurationBuilder.BuildConfiguration(values);

        var action = () => services.AddInfrastructure(
            configuration,
            enableSensitiveLogging: true,
            isTesting: false);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("PhotoStorage:LocalDevelopmentSigningKey must contain at least 32 UTF-8 bytes when the Local provider is enabled in Development.").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void AddInfrastructure_UsesConfiguredLocalSigningKeyInDevelopment()
    {
        var services = new ServiceCollection();
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values["PhotoStorage:Provider"] = "Local";
        values["PhotoStorage:LocalDevelopmentSigningKey"] = "task6-local-photo-test-key-32-bytes-minimum";
        var configuration = TestConfigurationBuilder.BuildConfiguration(values);

        services.AddInfrastructure(configuration, enableSensitiveLogging: true, isTesting: false);

        using var provider = services.BuildServiceProvider();
        Assert.That(provider.GetRequiredService<LocalPhotoDevelopmentUrlSigner>(), Is.Not.Null);
        Assert.That(provider.GetRequiredService<IPhotoStorageProvider>(), Is.TypeOf<LocalPhotoStorageProvider>());
    }

    [Test]
    public void AddInfrastructure_UsesCloudflareR2PhotoStorageProvider_WhenConfigured()
    {
        var services = new ServiceCollection();
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values["PhotoStorage:Provider"] = "CloudflareR2";
        values["PhotoStorage:BucketName"] = "lgym-report-photos-dev";
        values["PhotoStorage:Endpoint"] = "https://38c1c25f99af223efee28a9afcf5d575.r2.cloudflarestorage.com";
        values["PhotoStorage:AccessKeyId"] = "test-access-key";
        values["PhotoStorage:SecretAccessKey"] = "test-secret-key";
        var configuration = TestConfigurationBuilder.BuildConfiguration(values);

        services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: true);

        using var provider = services.BuildServiceProvider();
        Assert.That(provider.GetRequiredService<IPhotoStorageProvider>(), Is.TypeOf<CloudflareR2PhotoStorageProvider>());
    }

    [Test]
    public void AddInfrastructure_Throws_WhenProviderLocalOutsideDevelopmentOrTesting()
    {
        var services = new ServiceCollection();
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values["PhotoStorage:Provider"] = "Local";
        var configuration = TestConfigurationBuilder.BuildConfiguration(values);

        var action = () => services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: false);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("LocalPhotoStorageProvider cannot be used outside Development.").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    private static IConfiguration CreateInfrastructureConfiguration(string photoStorageProvider)
    {
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values["PhotoStorage:Provider"] = photoStorageProvider;
        values["PhotoStorage:BucketName"] = "lgym-report-photos-test";
        values["PhotoStorage:Endpoint"] = "https://example.r2.cloudflarestorage.com";
        values["PhotoStorage:AccessKeyId"] = "test-access-key";
        values["PhotoStorage:SecretAccessKey"] = "test-secret-key";

        return TestConfigurationBuilder.BuildConfiguration(values);
    }

    private static void AssertTypeDescriptor<TService, TImplementation>(
        IServiceCollection services,
        ServiceLifetime expectedLifetime)
    {
        AssertTypeDescriptor(services, typeof(TService), typeof(TImplementation), expectedLifetime);
    }

    private static void AssertTypeDescriptor(
        IServiceCollection services,
        Type serviceType,
        Type implementationType,
        ServiceLifetime expectedLifetime)
    {
        var descriptor = GetSingleDescriptor(services, serviceType);
        Assert.That(descriptor.Lifetime, Is.EqualTo(expectedLifetime));
        Assert.That(descriptor.ImplementationType, Is.EqualTo(implementationType));
        Assert.That(descriptor.ImplementationFactory, Is.Null);
        Assert.That(descriptor.ImplementationInstance, Is.Null);
    }

    private static void AssertFactoryDescriptor<TService>(
        IServiceCollection services,
        ServiceLifetime expectedLifetime)
    {
        var descriptor = GetSingleDescriptor(services, typeof(TService));
        Assert.That(descriptor.Lifetime, Is.EqualTo(expectedLifetime));
        Assert.That(descriptor.ImplementationFactory, Is.Not.Null);
        Assert.That(descriptor.ImplementationType, Is.Null);
        Assert.That(descriptor.ImplementationInstance, Is.Null);
    }

    private static void AssertInstanceDescriptor<TService>(
        IServiceCollection services,
        ServiceLifetime expectedLifetime)
    {
        var descriptor = GetSingleDescriptor(services, typeof(TService));
        Assert.That(descriptor.Lifetime, Is.EqualTo(expectedLifetime));
        Assert.That(descriptor.ImplementationInstance, Is.Not.Null);
        Assert.That(descriptor.ImplementationType, Is.Null);
        Assert.That(descriptor.ImplementationFactory, Is.Null);
    }

    private static ServiceDescriptor GetSingleDescriptor(IServiceCollection services, Type serviceType)
    {
        var assertedValue118 = (services
                    .Where(descriptor => descriptor.ServiceType == serviceType)).ToArray();
        Assert.That(assertedValue118, Has.Exactly(1).Items);
        return assertedValue118.Single();
    }

    private sealed class FakeInAppNotificationPushPublisher : IInAppNotificationPushPublisher
    {
        public Task PushAsync(InAppNotificationResult notification, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }

    private static IEnumerable<TestCaseData> FullHostPushCompositionManifest()
    {
        yield return new TestCaseData(new PushCompositionManifest(
            true,
            typeof(LgymApi.BackgroundWorker.Services.NoOpPushBackgroundScheduler),
            typeof(FcmPushSender),
            "Worker",
            "Notifications"));
        yield return new TestCaseData(new PushCompositionManifest(
            false,
            typeof(LgymApi.BackgroundWorker.Services.HangfirePushBackgroundScheduler),
            typeof(FcmPushSender),
            "Worker",
            "Notifications"));
    }

    public sealed record PushCompositionManifest(
        bool IsTesting,
        Type CurrentSchedulerImplementation,
        Type CurrentProviderImplementation,
        string FutureSchedulerSelector,
        string FutureProviderImplementationOwner);

    [Test]
    public void AddInfrastructure_FallsBackAppDefaults_WhenConfigurationInvalid()
    {
        var services = new ServiceCollection();
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values["AppDefaults:PreferredLanguage"] = "@@invalid-culture@@";
        values["AppDefaults:PreferredTimeZone"] = "Not/ARealTimeZone";
        var configuration = TestConfigurationBuilder.BuildConfiguration(values);

        services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: true);

        using var provider = services.BuildServiceProvider();
        var defaults = provider.GetRequiredService<AppDefaultsOptions>();

        Assert.That(defaults.PreferredLanguage, Is.EqualTo("en-US"));
        Assert.That(defaults.PreferredTimeZone, Is.EqualTo("Europe/Warsaw"));
    }

    [Test]
    public void AddNotificationsModule_UsesAppDefaultLanguage_WhenEmailDefaultCultureInvalid()
    {
        var services = new ServiceCollection();
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values["AppDefaults:PreferredLanguage"] = "pl-PL";
        values["Email:DefaultCulture"] = "@@invalid-culture@@";
        var configuration = TestConfigurationBuilder.BuildConfiguration(values);

        services.AddNotificationsModule(configuration);

        using var provider = services.BuildServiceProvider();
        var emailOptions = provider.GetRequiredService<EmailOptions>();

        Assert.That(emailOptions.DefaultCulture.Name, Is.EqualTo("pl-PL"));
    }

}
