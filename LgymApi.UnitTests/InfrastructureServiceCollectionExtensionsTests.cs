using FluentAssertions;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker;
using LgymApi.BackgroundWorker.Common;
using LgymApi.Application.Options;
using LgymApi.Infrastructure;
using LgymApi.Infrastructure.Options;
using LgymApi.Infrastructure.Services;
using LgymApi.TestUtils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class InfrastructureServiceCollectionExtensionsTests
{
     [Test]
     public void AddInfrastructure_RegistersNoOpScheduler_WhenTesting()
     {
         var configuration = TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>
         {
             ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test"
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
             ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test"
         });

         using var provider = TestServiceProviderFactory.CreateInfrastructureProvider(
             configuration,
             isTesting: true,
             includeBackgroundWorker: true);
         var dispatcher = provider.GetRequiredService<ICommandDispatcher>();
         dispatcher.Should().BeOfType<CommandDispatcher>();
     }

     [Test]
     public void AddBackgroundWorkerServices_RegistersNoOpScheduler_WhenTesting()
     {
         var services = new ServiceCollection();
         var configuration = TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>
         {
             ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test"
         });

         services.AddLogging();
         services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: true);
         services.AddBackgroundWorkerServices(isTesting: true);

         using var provider = services.BuildServiceProvider();
         var scheduler = provider.GetRequiredService<IActionMessageScheduler>();
         scheduler.Should().BeOfType<NoOpActionMessageScheduler>();
     }

     [Test]
     public void AddBackgroundWorkerServices_RegistersHangfireScheduler_WhenNotTesting()
     {
         var services = new ServiceCollection();
         var configuration = TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>
         {
             ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test"
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
