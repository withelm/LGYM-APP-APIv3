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
        Assert.That(scheduler, Is.TypeOf<NoOpEmailBackgroundScheduler>());
    }

    [Test]
    public void AddInfrastructure_UsesSmtpDeliveryModeByDefault()
    {
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values.Remove("Email:DeliveryMode");
        var configuration = TestConfigurationBuilder.BuildConfiguration(values);

        using var provider = TestServiceProviderFactory.CreateInfrastructureProvider(configuration, isTesting: true);
        var sender = provider.GetRequiredService<IEmailSender>();
        Assert.That(sender, Is.TypeOf<SmtpEmailSender>());
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
        Assert.That(sender, Is.TypeOf<DummyEmailSender>());
    }

    [Test]
    public void AddInfrastructure_Throws_WhenDeliveryModeInvalid()
    {
        var services = new ServiceCollection();
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values["Email:DeliveryMode"] = "something-else";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(TestConfigurationBuilder.BuildConfiguration(values), enableSensitiveLogging: false, isTesting: true));

        Assert.That(exception!.Message, Is.EqualTo("Email:DeliveryMode must be one of: Smtp, Dummy."));
    }

    [Test]
    public void AddInfrastructure_Throws_WhenDummyOutputDirectoryMissingInDummyMode()
    {
        var services = new ServiceCollection();
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values["Email:DeliveryMode"] = "Dummy";
        values["Email:DummyOutputDirectory"] = "   ";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(TestConfigurationBuilder.BuildConfiguration(values), enableSensitiveLogging: false, isTesting: true));

        Assert.That(exception!.Message, Is.EqualTo("Email:DummyOutputDirectory is required when Email:DeliveryMode is Dummy."));
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

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(TestConfigurationBuilder.BuildConfiguration(values), enableSensitiveLogging: false, isTesting: true));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Message, Is.EqualTo(expectedMessage));
    }

    [Test]
    public void AddInfrastructure_Throws_WhenTemplateRootPathMissing()
    {
        var services = new ServiceCollection();
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values["Email:TemplateRootPath"] = "";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(TestConfigurationBuilder.BuildConfiguration(values), enableSensitiveLogging: false, isTesting: true));

        Assert.That(exception!.Message, Is.EqualTo("Email:TemplateRootPath is required when email is enabled."));
    }

    [Test]
    public void AddInfrastructure_Throws_WhenFromAddressInvalid()
    {
        var services = new ServiceCollection();
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values["Email:FromAddress"] = "invalid-email";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(TestConfigurationBuilder.BuildConfiguration(values), enableSensitiveLogging: false, isTesting: true));

        Assert.That(exception!.Message, Is.EqualTo("Email:FromAddress must be a valid email address."));
    }

    [Test]
    public void AddInfrastructure_Throws_WhenSmtpHostMissing()
    {
        var services = new ServiceCollection();
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values["Email:SmtpHost"] = "";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(TestConfigurationBuilder.BuildConfiguration(values), enableSensitiveLogging: false, isTesting: true));

        Assert.That(exception!.Message, Is.EqualTo("Email:SmtpHost is required when email is enabled."));
    }

    [Test]
    public void AddInfrastructure_Throws_WhenSmtpPortNonPositive()
    {
        var services = new ServiceCollection();
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values["Email:SmtpPort"] = "0";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(TestConfigurationBuilder.BuildConfiguration(values), enableSensitiveLogging: false, isTesting: true));

        Assert.That(exception!.Message, Is.EqualTo("Email:SmtpPort must be greater than 0 when email is enabled."));
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
        Assert.That(dispatcher, Is.TypeOf<CommandDispatcher>());
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
        Assert.That(scheduler, Is.TypeOf<NoOpActionMessageScheduler>());
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
        Assert.That(scheduler, Is.TypeOf<HangfireActionMessageScheduler>());
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
        Assert.That(orchestrator, Is.Not.Null);
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

        Assert.That(emailOptions.DefaultCulture.Name, Is.EqualTo("pl-PL"));
    }

}
