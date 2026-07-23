using FluentAssertions;
using LgymApi.Application.Coaching;
using LgymApi.Application.Coaching.Contracts.BackgroundCommands;
using LgymApi.Application.Identity;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker;
using LgymApi.BackgroundWorker.Common.Jobs;
using LgymApi.Application.Options;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Contracts.Events;
using LgymApi.Application.Notifications.Contracts.Push;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Notifications.Repositories;
using LgymApi.Application.Repositories;
using LgymApi.Infrastructure;
using LgymApi.Infrastructure.Options;
using LgymApi.Infrastructure.Services;
using LgymApi.BackgroundWorker.Actions;
using LgymApi.BackgroundWorker.Actions.Contracts;
using LgymApi.BackgroundWorker.Notifications;
using LgymApi.Application.Abstractions.Storage;
using LgymApi.TestUtils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ApplicationCommandDispatcher = LgymApi.Application.Platform.Contracts.BackgroundCommands.ICommandDispatcher;
using IActionMessageScheduler = LgymApi.BackgroundWorker.Common.IActionMessageScheduler;
using IEmailBackgroundScheduler = LgymApi.BackgroundWorker.Common.IEmailBackgroundScheduler;

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

         using var provider = TestServiceProviderFactory.CreateInfrastructureProvider(
             configuration,
             isTesting: true,
             includeBackgroundWorker: true);
         var scheduler = provider.GetRequiredService<IEmailBackgroundScheduler>();
         scheduler.Should().BeOfType<NoOpEmailBackgroundScheduler>();
     }

     [Test]
     public void AddInfrastructure_UsesSmtpDeliveryModeByDefault()
     {
         var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
         values.Remove("Email:DeliveryMode");
         var configuration = TestConfigurationBuilder.BuildConfiguration(values);

         using var provider = TestServiceProviderFactory.CreateInfrastructureProvider(configuration, isTesting: true);
         var sender = provider.GetRequiredService<IEmailSender>();
         sender.Should().BeOfType<SmtpEmailSender>();
     }

     [Test]
     public void AddInfrastructure_UsesDummyEmailSender_WhenModeIsDummy()
     {
         var services = new ServiceCollection();
         var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
         values["Email:DeliveryMode"] = "Dummy";
         values["Email:DummyOutputDirectory"] = "DummyOutbox";
         values["Email:SmtpHost"] = string.Empty;
         var configuration = TestConfigurationBuilder.BuildConfiguration(values);

         services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: true);

         using var provider = services.BuildServiceProvider();
         var sender = provider.GetRequiredService<IEmailSender>();
         sender.Should().BeOfType<DummyEmailSender>();
     }

     [Test]
     public void AddInfrastructure_Throws_WhenDeliveryModeInvalid()
     {
         var services = new ServiceCollection();
         var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
         values["Email:DeliveryMode"] = "something-else";

         var action = () =>
             services.AddInfrastructure(TestConfigurationBuilder.BuildConfiguration(values), enableSensitiveLogging: false, isTesting: true);

         action.Should()
             .Throw<InvalidOperationException>()
             .WithMessage("Email:DeliveryMode must be one of: Smtp, Dummy.");
     }

     [Test]
     public void AddInfrastructure_Throws_WhenDummyOutputDirectoryMissingInDummyMode()
     {
         var services = new ServiceCollection();
         var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
         values["Email:DeliveryMode"] = "Dummy";
         values["Email:DummyOutputDirectory"] = "   ";

         var action = () =>
             services.AddInfrastructure(TestConfigurationBuilder.BuildConfiguration(values), enableSensitiveLogging: false, isTesting: true);

         action.Should()
             .Throw<InvalidOperationException>()
             .WithMessage("Email:DummyOutputDirectory is required when Email:DeliveryMode is Dummy.");
     }

     [TestCase(null, "Email:InvitationBaseUrl is required.")]
     [TestCase("not-an-url", "Email:InvitationBaseUrl must be a valid absolute URL.")]
     public void AddInfrastructure_Throws_ForInvalidInvitationBaseUrl(string? invitationBaseUrl, string expectedMessage)
     {
         var services = new ServiceCollection();
         var configuration = TestConfigurationBuilder.BuildEnabledEmailConfiguration();
         var values = new Dictionary<string, string?>(configuration.AsEnumerable().ToDictionary(k => k.Key, v => v.Value))
         {
             ["Email:InvitationBaseUrl"] = invitationBaseUrl
         };

         var action = () =>
             services.AddInfrastructure(TestConfigurationBuilder.BuildConfiguration(values), enableSensitiveLogging: false, isTesting: true);

         action.Should()
             .Throw<InvalidOperationException>()
             .WithMessage(expectedMessage);
     }

     [Test]
     public void AddInfrastructure_Throws_WhenTemplateRootPathMissing()
     {
         var services = new ServiceCollection();
         var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
         values["Email:TemplateRootPath"] = "";

         var action = () =>
             services.AddInfrastructure(TestConfigurationBuilder.BuildConfiguration(values), enableSensitiveLogging: false, isTesting: true);

         action.Should()
             .Throw<InvalidOperationException>()
             .WithMessage("Email:TemplateRootPath is required when email is enabled.");
     }

     [Test]
     public void AddInfrastructure_Throws_WhenFromAddressInvalid()
     {
         var services = new ServiceCollection();
         var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
         values["Email:FromAddress"] = "invalid-email";

         var action = () =>
             services.AddInfrastructure(TestConfigurationBuilder.BuildConfiguration(values), enableSensitiveLogging: false, isTesting: true);

         action.Should()
             .Throw<InvalidOperationException>()
             .WithMessage("Email:FromAddress must be a valid email address.");
     }

     [Test]
     public void AddInfrastructure_Throws_WhenSmtpHostMissing()
     {
         var services = new ServiceCollection();
         var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
         values["Email:SmtpHost"] = "";

         var action = () =>
             services.AddInfrastructure(TestConfigurationBuilder.BuildConfiguration(values), enableSensitiveLogging: false, isTesting: true);

         action.Should()
             .Throw<InvalidOperationException>()
             .WithMessage("Email:SmtpHost is required when email is enabled.");
     }

     [Test]
     public void AddInfrastructure_Throws_WhenSmtpPortNonPositive()
     {
         var services = new ServiceCollection();
         var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
         values["Email:SmtpPort"] = "0";

         var action = () =>
             services.AddInfrastructure(TestConfigurationBuilder.BuildConfiguration(values), enableSensitiveLogging: false, isTesting: true);

         action.Should()
             .Throw<InvalidOperationException>()
             .WithMessage("Email:SmtpPort must be greater than 0 when email is enabled.");
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

         using var provider = TestServiceProviderFactory.CreateInfrastructureProvider(
             configuration,
             isTesting: true,
             includeBackgroundWorker: true);
         var dispatcher = provider.GetRequiredService<ApplicationCommandDispatcher>();
         dispatcher.Should().BeOfType<CommandDispatcher>();
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
         services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: true);
         services.AddNotificationsModule(configuration);
         services.AddBackgroundWorkerServices(isTesting: true);
         services.AddScoped<IInAppNotificationPushPublisher, FakeInAppNotificationPushPublisher>();

       using var provider = services.BuildServiceProvider();
        var scheduler = provider.GetRequiredService<IActionMessageScheduler>();
        scheduler.Should().BeOfType<NoOpActionMessageScheduler>();
        provider.GetRequiredService<IPushBackgroundScheduler>().Should().BeOfType<LgymApi.BackgroundWorker.Services.NoOpPushBackgroundScheduler>();
         provider.GetRequiredService<INotificationEventBridge>().Should().BeOfType<NotificationEventBridge>();
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
          services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: true);
          services.AddBackgroundWorkerServices(isTesting: true);
          services.AddScoped<IInAppNotificationPushPublisher, FakeInAppNotificationPushPublisher>();

           services.Count(descriptor => descriptor.ServiceType == typeof(IPushInstallationRepository)).Should().Be(1);
           services.Count(descriptor => descriptor.ServiceType == typeof(IPushNotificationMessageRepository)).Should().Be(1);
           services.Count(descriptor => descriptor.ServiceType == typeof(IInAppNotificationRepository)).Should().Be(1);
           services.Count(descriptor => descriptor.ServiceType == typeof(IPushBackgroundScheduler)).Should().Be(1);
           services.Count(descriptor => descriptor.ServiceType == typeof(IPushProviderSender)).Should().Be(1);
       }

      [Test]
      public void AddNotificationsModule_RegistersApplicationAndInfrastructureWithoutWorkerScheduler()
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

          services.AddNotificationsModule(configuration);

          services.Count(descriptor => descriptor.ServiceType == typeof(IInAppNotificationService)).Should().Be(1);
           services.Count(descriptor => descriptor.ServiceType == typeof(INotificationEventBridge)).Should().Be(1);
           services.Count(descriptor => descriptor.ServiceType == typeof(IPushInstallationRepository)).Should().Be(1);
           services.Count(descriptor => descriptor.ServiceType == typeof(IPushNotificationMessageRepository)).Should().Be(1);
           services.Count(descriptor => descriptor.ServiceType == typeof(IInAppNotificationRepository)).Should().Be(1);
           services.Count(descriptor => descriptor.ServiceType == typeof(IPushBackgroundScheduler)).Should().Be(0);
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

          services.AddNotificationsModule(configuration);
          services.AddBackgroundWorkerServices(isTesting);

          services.Count(descriptor => descriptor.ServiceType == typeof(IInAppNotificationService)).Should().Be(1);
          var bridge = services
              .Where(descriptor => descriptor.ServiceType == typeof(INotificationEventBridge))
              .Should()
              .ContainSingle()
              .Which;
           bridge.ImplementationType.Should().Be(typeof(NotificationEventBridge));
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

          scopedServices.GetServices<ICoachingEmailNotificationFeature>().Should().ContainSingle()
              .Which.Should().BeOfType<CoachingEmailNotificationSchedulerAdapter>();
          scopedServices.GetServices<ICoachingEmailNotificationScheduler>().Should().ContainSingle()
              .Which.Should().BeOfType<CoachingEmailNotificationSchedulerAdapter>();
          scopedServices.GetRequiredService<IEmailBackgroundScheduler>().Should().BeOfType(expectedBackgroundScheduler);
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

          var schedulerDescriptor = services
              .Where(descriptor => descriptor.ServiceType == typeof(IPushBackgroundScheduler))
              .Should()
              .ContainSingle()
              .Which;
          var providerDescriptor = services
              .Where(descriptor => descriptor.ServiceType == typeof(IPushProviderSender))
              .Should()
              .ContainSingle()
              .Which;

          schedulerDescriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
          schedulerDescriptor.ImplementationType.Should().Be(expectation.CurrentSchedulerImplementation);
          providerDescriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
          providerDescriptor.ImplementationType.Should().Be(expectation.CurrentProviderImplementation);
          expectation.FutureSchedulerSelector.Should().Be("Worker");
          expectation.FutureProviderImplementationOwner.Should().Be("Infrastructure");
      }

      [Test]
      public void AddInfrastructure_Throws_WhenPushSendsEnabledWithoutProjectId()
       {
           var services = new ServiceCollection();
           var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
           values["PushNotifications:SendEnabled"] = "true";
           values["PushNotifications:Fcm:CredentialsJson"] = "{ }";

           var action = () => services.AddInfrastructure(TestConfigurationBuilder.BuildConfiguration(values), enableSensitiveLogging: false, isTesting: true);

          action.Should()
               .Throw<InvalidOperationException>()
               .WithMessage("PushNotifications:Fcm:ProjectId is required when push notifications are enabled.");
       }

      [Test]
      public void AddInfrastructure_DoesNotRequireFcmCredentials_WhenPushSendsDisabled()
      {
          var services = new ServiceCollection();
          var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
          values["PushNotifications:SendEnabled"] = "false";
          values["PushNotifications:StaleTokenCleanupEnabled"] = "true";

          var action = () => services.AddInfrastructure(TestConfigurationBuilder.BuildConfiguration(values), enableSensitiveLogging: false, isTesting: true);

          action.Should().NotThrow();
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
         scheduler.Should().BeOfType<HangfireActionMessageScheduler>();
     }

     [Test]
     public void AddBackgroundWorkerServices_RegistersOrchestratorService()
     {
         var configuration = TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>
         {
             ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test"
         });

         using var provider = TestServiceProviderFactory.CreateInfrastructureProvider(
             configuration,
             isTesting: true,
             includeBackgroundWorker: true);
         var orchestrator = provider.GetRequiredService<BackgroundActionOrchestratorService>();
         orchestrator.Should().NotBeNull();
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

         using var provider = TestServiceProviderFactory.CreateInfrastructureProvider(
             configuration,
             isTesting: true,
             includeBackgroundWorker: true);

         var job = provider.GetRequiredService<ICommittedIntentDispatchJob>();
         job.Should().BeOfType<LgymApi.BackgroundWorker.Jobs.CommittedIntentDispatchJob>();
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
         services.AddApplicationMapping(typeof(IMappingProfile).Assembly);
         services.AddIdentityModule();
         services.AddCoachingModule();
         services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: true);
         services.AddNotificationsModule(configuration);
         services.AddBackgroundWorkerServices(isTesting: true);
         services.AddScoped<IInAppNotificationPushPublisher, FakeInAppNotificationPushPublisher>();

         using var provider = services.BuildServiceProvider();

         provider.GetRequiredService<IBackgroundAction<TrainerInvitationCreatedInAppNotificationCommand>>()
             .Should().BeOfType<TrainerInvitationCreatedInAppNotificationCommandHandler>();
         provider.GetRequiredService<IBackgroundAction<TrainerInvitationAcceptedInAppNotificationCommand>>()
             .Should().BeOfType<TrainerInvitationAcceptedInAppNotificationCommandHandler>();
         provider.GetRequiredService<IBackgroundAction<TrainerInvitationRejectedInAppNotificationCommand>>()
             .Should().BeOfType<TrainerInvitationRejectedInAppNotificationCommandHandler>();
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

         defaults.PreferredLanguage.Should().Be("pl-PL");
         defaults.PreferredTimeZone.Should().Be("UTC");
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
         provider.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>().Should().NotBeNull();
         provider.GetRequiredService<IPhotoStorageProvider>().Should().BeOfType<LocalPhotoStorageProvider>();
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
         provider.GetRequiredService<IPhotoStorageProvider>().Should().BeOfType<CloudflareR2PhotoStorageProvider>();
     }

     [Test]
     public void AddInfrastructure_Throws_WhenProviderLocalOutsideDevelopmentOrTesting()
     {
         var services = new ServiceCollection();
         var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
         values["PhotoStorage:Provider"] = "Local";
         var configuration = TestConfigurationBuilder.BuildConfiguration(values);

         var action = () => services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: false);

         action.Should()
             .Throw<InvalidOperationException>()
             .WithMessage("LocalPhotoStorageProvider cannot be used outside Development.");
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
              "Infrastructure"));
          yield return new TestCaseData(new PushCompositionManifest(
              false,
              typeof(LgymApi.BackgroundWorker.Services.HangfirePushBackgroundScheduler),
              typeof(FcmPushSender),
              "Worker",
              "Infrastructure"));
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

         defaults.PreferredLanguage.Should().Be("en-US");
         defaults.PreferredTimeZone.Should().Be("Europe/Warsaw");
     }

     [Test]
     public void AddInfrastructure_UsesAppDefaultLanguage_WhenEmailDefaultCultureInvalid()
     {
         var services = new ServiceCollection();
         var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
         values["AppDefaults:PreferredLanguage"] = "pl-PL";
         values["Email:DefaultCulture"] = "@@invalid-culture@@";
         var configuration = TestConfigurationBuilder.BuildConfiguration(values);

         services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: true);

         using var provider = services.BuildServiceProvider();
         var emailOptions = provider.GetRequiredService<EmailOptions>();

         emailOptions.DefaultCulture.Name.Should().Be("pl-PL");
     }

}
