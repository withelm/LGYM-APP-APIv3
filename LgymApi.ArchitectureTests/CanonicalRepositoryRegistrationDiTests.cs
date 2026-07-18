using LgymApi.Application.Repositories;
using LgymApi.Infrastructure;
using LgymApi.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class CanonicalRepositoryRegistrationDiTests
{
    [Test]
    public void AddInfrastructure_Should_Register_Canonical_Repositories_Once_AsScoped_And_Resolve_Them()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test",
                ["Email:Enabled"] = "true",
                ["Email:InvitationBaseUrl"] = "https://example.com/invite",
                ["Email:PasswordRecoveryBaseUrl"] = "https://example.com/reset",
                ["Email:TemplateRootPath"] = "EmailTemplates",
                ["Email:DefaultCulture"] = "en-US",
                ["Email:FromAddress"] = "coach@example.com",
                ["Email:SmtpHost"] = "smtp.example.com",
                ["Email:SmtpPort"] = "587"
            })
            .Build();

        services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: true);

        var expectedRegistrations = new[]
        {
            (Module: "WorkoutProgress", ServiceType: typeof(IEloRegistryRepository), ImplementationType: typeof(EloRegistryRepository)),
            (Module: "WorkoutProgress", ServiceType: typeof(IMainRecordRepository), ImplementationType: typeof(MainRecordRepository)),
            (Module: "Notifications", ServiceType: typeof(IEmailNotificationLogRepository), ImplementationType: typeof(EmailNotificationLogRepository)),
            (Module: "Notifications", ServiceType: typeof(IEmailNotificationSubscriptionRepository), ImplementationType: typeof(EmailNotificationSubscriptionRepository))
        };

        var emailLogDescriptor = services.Single(descriptor => descriptor.ServiceType == typeof(IEmailNotificationLogRepository));
        Assert.That(emailLogDescriptor.ImplementationFactory, Is.Not.Null, "Email notification log repository must retain its factory registration.");

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        foreach (var expected in expectedRegistrations)
        {
            var descriptors = services.Where(descriptor => descriptor.ServiceType == expected.ServiceType).ToList();

            Assert.That(descriptors, Has.Count.EqualTo(1), $"Expected one registration for {expected.ServiceType.Name}.");
            Assert.That(descriptors.Single().Lifetime, Is.EqualTo(ServiceLifetime.Scoped), $"Expected scoped lifetime for {expected.ServiceType.Name}.");

            var resolved = scope.ServiceProvider.GetRequiredService(expected.ServiceType);
            Assert.That(resolved.GetType(), Is.EqualTo(expected.ImplementationType), $"Unexpected implementation for {expected.ServiceType.Name}.");

            TestContext.Progress.WriteLine(
                $"module={expected.Module}; service={expected.ServiceType.Name}; lifetime={descriptors.Single().Lifetime}; implementation={resolved.GetType().Name}");
        }
    }

    [TestCase("/LgymApi.Application/Repositories/IEloRegistryRepository.cs", "Workout & Progress")]
    [TestCase("/LgymApi.Application/Repositories/IMainRecordRepository.cs", "Workout & Progress")]
    [TestCase("/LgymApi.Application/Repositories/IEmailNotificationLogRepository.cs", "Notifications")]
    [TestCase("/LgymApi.Application/Repositories/IEmailNotificationSubscriptionRepository.cs", "Notifications")]
    [TestCase("/LgymApi.Application/EloRegistry/EloRegistryService.cs", "Workout & Progress")]
    [TestCase("/LgymApi.Application/MainRecords/MainRecordsService.cs", "Workout & Progress")]
    [TestCase("/LgymApi.Infrastructure/Repositories/EloRegistryRepository.cs", "Workout & Progress")]
    [TestCase("/LgymApi.Infrastructure/Repositories/MainRecordRepository.cs", "Workout & Progress")]
    [TestCase("/LgymApi.Infrastructure/Repositories/EmailNotificationLogRepository.cs", "Notifications")]
    [TestCase("/LgymApi.Infrastructure/Repositories/EmailNotificationSubscriptionRepository.cs", "Notifications")]
    public void ArchitectureTestHelpers_Should_Resolve_Canonical_Repository_Owners(string path, string expectedModule)
    {
        Assert.That(ArchitectureTestHelpers.GetCanonicalModuleNameFromPath(path), Is.EqualTo(expectedModule));
    }
}
