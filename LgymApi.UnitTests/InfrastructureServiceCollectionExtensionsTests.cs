using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.BackgroundWorker.Actions;
using LgymApi.Infrastructure;
using LgymApi.Application;
using LgymApi.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class InfrastructureServiceCollectionExtensionsTests
{
    [Test]
    public void AddInfrastructure_RegistersNoOpScheduler_WhenTesting()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test"
        });

        services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: true);
        services.AddBackgroundWorkerServices(isTesting: true);

        using var provider = services.BuildServiceProvider();
        var scheduler = provider.GetRequiredService<IEmailBackgroundScheduler>();
        Assert.That(scheduler, Is.TypeOf<NoOpEmailBackgroundScheduler>());
    }

    [Test]
    public void AddInfrastructure_UsesSmtpDeliveryModeByDefault()
    {
        var services = new ServiceCollection();
        var values = ToDictionary(BuildEnabledEmailConfiguration());
        values.Remove("Email:DeliveryMode");
        var configuration = BuildConfiguration(values);

        services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: true);

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<IEmailSender>();
        Assert.That(sender, Is.TypeOf<SmtpEmailSender>());
    }

    [Test]
    public void AddInfrastructure_UsesDummyEmailSender_WhenModeIsDummy()
    {
        var services = new ServiceCollection();
        var values = ToDictionary(BuildEnabledEmailConfiguration());
        values["Email:DeliveryMode"] = "Dummy";
        values["Email:DummyOutputDirectory"] = "DummyOutbox";
        values["Email:SmtpHost"] = string.Empty;
        var configuration = BuildConfiguration(values);

        services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: true);

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<IEmailSender>();
        Assert.That(sender, Is.TypeOf<DummyEmailSender>());
    }

    [Test]
    public void AddInfrastructure_Throws_WhenDeliveryModeInvalid()
    {
        var services = new ServiceCollection();
        var values = ToDictionary(BuildEnabledEmailConfiguration());
        values["Email:DeliveryMode"] = "something-else";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(BuildConfiguration(values), enableSensitiveLogging: false, isTesting: true));

        Assert.That(exception!.Message, Is.EqualTo("Email:DeliveryMode must be one of: Smtp, Dummy."));
    }

    [Test]
    public void AddInfrastructure_Throws_WhenDummyOutputDirectoryMissingInDummyMode()
    {
        var services = new ServiceCollection();
        var values = ToDictionary(BuildEnabledEmailConfiguration());
        values["Email:DeliveryMode"] = "Dummy";
        values["Email:DummyOutputDirectory"] = "   ";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(BuildConfiguration(values), enableSensitiveLogging: false, isTesting: true));

        Assert.That(exception!.Message, Is.EqualTo("Email:DummyOutputDirectory is required when Email:DeliveryMode is Dummy."));
    }

    [TestCase(null, "Email:InvitationBaseUrl is required.")]
    [TestCase("not-an-url", "Email:InvitationBaseUrl must be a valid absolute URL.")]
    public void AddInfrastructure_Throws_ForInvalidInvitationBaseUrl(string? invitationBaseUrl, string expectedMessage)
    {
        var services = new ServiceCollection();
        var configuration = BuildEnabledEmailConfiguration();
        var values = new Dictionary<string, string?>(configuration.AsEnumerable().ToDictionary(k => k.Key, v => v.Value))
        {
            ["Email:InvitationBaseUrl"] = invitationBaseUrl
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(BuildConfiguration(values), enableSensitiveLogging: false, isTesting: true));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Message, Is.EqualTo(expectedMessage));
    }

    [Test]
    public void AddInfrastructure_Throws_WhenTemplateRootPathMissing()
    {
        var services = new ServiceCollection();
        var values = ToDictionary(BuildEnabledEmailConfiguration());
        values["Email:TemplateRootPath"] = "";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(BuildConfiguration(values), enableSensitiveLogging: false, isTesting: true));

        Assert.That(exception!.Message, Is.EqualTo("Email:TemplateRootPath is required when email is enabled."));
    }

    [Test]
    public void AddInfrastructure_Throws_WhenFromAddressInvalid()
    {
        var services = new ServiceCollection();
        var values = ToDictionary(BuildEnabledEmailConfiguration());
        values["Email:FromAddress"] = "invalid-email";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(BuildConfiguration(values), enableSensitiveLogging: false, isTesting: true));

        Assert.That(exception!.Message, Is.EqualTo("Email:FromAddress must be a valid email address."));
    }

    [Test]
    public void AddInfrastructure_Throws_WhenSmtpHostMissing()
    {
        var services = new ServiceCollection();
        var values = ToDictionary(BuildEnabledEmailConfiguration());
        values["Email:SmtpHost"] = "";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(BuildConfiguration(values), enableSensitiveLogging: false, isTesting: true));

        Assert.That(exception!.Message, Is.EqualTo("Email:SmtpHost is required when email is enabled."));
    }

    [Test]
    public void AddInfrastructure_Throws_WhenSmtpPortNonPositive()
    {
        var services = new ServiceCollection();
        var values = ToDictionary(BuildEnabledEmailConfiguration());
        values["Email:SmtpPort"] = "0";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(BuildConfiguration(values), enableSensitiveLogging: false, isTesting: true));

        Assert.That(exception!.Message, Is.EqualTo("Email:SmtpPort must be greater than 0 when email is enabled."));
    }

    [Test]
    public void AddBackgroundWorkerServices_RegistersCommandDispatcher()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test"
        });

        services.AddLogging();
        services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: true);
        services.AddBackgroundWorkerServices(isTesting: true);

        using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<ICommandDispatcher>();
        Assert.That(dispatcher, Is.TypeOf<CommandDispatcher>());
    }

    [Test]
    public void AddBackgroundWorkerServices_RegistersNoOpScheduler_WhenTesting()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
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
        var configuration = BuildConfiguration(new Dictionary<string, string?>
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
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test"
        });

        services.AddLogging();
        services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: true);
        services.AddBackgroundWorkerServices(isTesting: true);

        using var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<BackgroundActionOrchestratorService>();
        Assert.That(orchestrator, Is.Not.Null);
    }

    [Test]
    public void AddBackgroundWorkerServices_RegistersTypedCommandHandlers()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test"
        });

services.AddLogging();
        services.AddApplicationServices();
        services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: true);
        services.AddBackgroundWorkerServices(isTesting: true);
        services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: true);
        services.AddBackgroundWorkerServices(isTesting: true);

        using var provider = services.BuildServiceProvider();
        
        // Verify UserRegisteredCommand handlers are registered
        var userRegisteredHandlers = provider.GetServices<IBackgroundAction<UserRegisteredCommand>>();
        Assert.That(userRegisteredHandlers, Is.Not.Empty, "UserRegisteredCommand handlers should be registered");
        Assert.That(userRegisteredHandlers.OfType<UserRegisteredCommandHandler>().Count(), Is.GreaterThan(0));
        
        // Verify InvitationCreatedCommand handlers are registered
        var invitationHandlers = provider.GetServices<IBackgroundAction<InvitationCreatedCommand>>();
        Assert.That(invitationHandlers, Is.Not.Empty, "InvitationCreatedCommand handlers should be registered");
        Assert.That(invitationHandlers.OfType<InvitationCreatedCommandHandler>().Count(), Is.GreaterThan(0));
        
        // Verify TrainingCompletedCommand handlers are registered (main-record)
        var trainingHandlers = provider.GetServices<IBackgroundAction<TrainingCompletedCommand>>();
        Assert.That(trainingHandlers, Is.Not.Empty, "TrainingCompletedCommand handlers should be registered");
        Assert.That(trainingHandlers.OfType<TrainingCompletedMainRecordCommandHandler>().Count(), Is.GreaterThan(0));
    }

    private static IConfiguration BuildEnabledEmailConfiguration()
    {
        return BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test",
            ["Email:Enabled"] = "true",
            ["Email:InvitationBaseUrl"] = "https://example.com/invite",
            ["Email:TemplateRootPath"] = "EmailTemplates",
            ["Email:DefaultCulture"] = "en-US",
            ["Email:FromAddress"] = "coach@example.com",
            ["Email:SmtpHost"] = "smtp.example.com",
            ["Email:SmtpPort"] = "587"
        });
    }

    private static Dictionary<string, string?> ToDictionary(IConfiguration configuration)
    {
        return configuration.AsEnumerable().ToDictionary(x => x.Key, x => x.Value);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
