using System.Reflection;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Repositories;
using LgymApi.Application.Notifications.Providers.Fcm;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common.Jobs;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.TestUtils;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class PushNotificationRetentionContractTests
{
    [Test]
    public void PushInstallationRepository_WhenSelectingStaleActiveCandidates_UsesCallerSuppliedCutoffAndLimit()
    {
        var method = typeof(IPushInstallationRepository).GetMethod(
            nameof(IPushInstallationRepository.GetStaleActiveAsync),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        Assert.That(method!.ReturnType, Is.EqualTo(typeof(Task<List<PushInstallation>>)));
        Assert.That(method.GetParameters().Select(parameter => parameter.ParameterType), Is.EqualTo(new[] { typeof(DateTimeOffset), typeof(int), typeof(IReadOnlyList<Id<AccountReference>>), typeof(CancellationToken) }));
    }

    [Test]
    public void RetentionRepositories_WhenSelectingAndDeletingCandidates_DeclareTimestampSpecificBoundedStageOnlyPorts()
    {
        AssertRetentionCandidateContract<IPushNotificationMessageRepository, PushNotificationMessage>(
            "GetRetentionCandidatesCreatedBeforeAsync");
        AssertRetentionCandidateContract<IPushInstallationRepository, PushInstallation>(
            "GetRetentionCandidatesDisabledBeforeAsync");
        AssertRetentionCandidateContract<IInAppNotificationRepository, InAppNotification>(
            "GetRetentionCandidatesCreatedBeforeAsync");
    }

    [Test]
    public void PushNotificationOptionsFactory_WhenExistingValuesAreMissingOrInvalid_UsesEstablishedDefaultsAndNormalization()
    {
        var configuration = TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>
        {
            ["PushNotifications:RetryDelaysSeconds:0"] = "0",
            ["PushNotifications:RetryDelaysSeconds:1"] = "-5",
            ["PushNotifications:StaleTokenInactivityDays"] = "0",
            ["PushNotifications:StaleTokenCleanupBatchSize"] = "-1",
            ["PushNotifications:Fcm:ProjectId"] = " project-id ",
            ["PushNotifications:Fcm:CredentialsPath"] = " path.json ",
            ["PushNotifications:Fcm:CredentialsJson"] = "  ",
            ["PushNotifications:Fcm:BaseUrl"] = " https://fcm.example.test/ "
        });

        var options = PushNotificationOptionsFactory.Create(configuration);

        Assert.That(options.IsSendEnabled, Is.False);
        Assert.That(options.RetryDelaysSeconds, Is.EqualTo(new[] { 30, 120, 600 }));
        Assert.That(options.StaleTokenInactivityDays, Is.EqualTo(45));
        Assert.That(options.StaleTokenCleanupBatchSize, Is.EqualTo(500));
        Assert.That(options.Fcm.ProjectId, Is.EqualTo("project-id"));
        Assert.That(options.Fcm.CredentialsPath, Is.EqualTo("path.json"));
        Assert.That(options.Fcm.CredentialsJson, Is.Null);
        Assert.That(options.Fcm.BaseUrl, Is.EqualTo("https://fcm.example.test"));
    }

    [Test]
    public void PushNotificationOptionsFactory_WhenRetentionValuesAreMissing_UsesApprovedDefaults()
    {
        var options = PushNotificationOptionsFactory.Create(
            TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>()));

        Assert.That(options.MessageHistoryDays, Is.EqualTo(30));
        Assert.That(options.DisabledInstallationDays, Is.EqualTo(30));
        Assert.That(options.InAppNotificationDays, Is.EqualTo(90));
        Assert.That(options.RetentionPurgeBatchSize, Is.EqualTo(500));
    }

    [Test]
    public void PushNotificationOptionsFactory_WhenRetentionValuesAreInvalid_NormalizesPositiveValuesAndRejectsNonNumericValues()
    {
        var configuration = TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>
        {
            ["PushNotifications:MessageHistoryDays"] = "0",
            ["PushNotifications:DisabledInstallationDays"] = "-1",
            ["PushNotifications:InAppNotificationDays"] = "0",
            ["PushNotifications:RetentionPurgeBatchSize"] = "-1"
        });

        var options = PushNotificationOptionsFactory.Create(configuration);
        Assert.That(options.MessageHistoryDays, Is.EqualTo(30));
        Assert.That(options.DisabledInstallationDays, Is.EqualTo(30));
        Assert.That(options.InAppNotificationDays, Is.EqualTo(90));
        Assert.That(options.RetentionPurgeBatchSize, Is.EqualTo(500));

        var invalidConfiguration = TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>
        {
            ["PushNotifications:MessageHistoryDays"] = "not-an-integer"
        });

        var action = () => PushNotificationOptionsFactory.Create(invalidConfiguration);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public void NotificationRetentionSettings_ProjectsConfiguredValuesAndRetentionJobsRemainNarrow()
    {
        var options = PushNotificationOptionsFactory.Create(
            TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>
            {
                ["PushNotifications:MessageHistoryDays"] = "31",
                ["PushNotifications:DisabledInstallationDays"] = "32",
                ["PushNotifications:InAppNotificationDays"] = "91",
                ["PushNotifications:RetentionPurgeBatchSize"] = "250"
            }));
        INotificationRetentionSettings settings = new NotificationRetentionSettings(options);

        Assert.That(settings.MessageHistoryDays, Is.EqualTo(31));
        Assert.That(settings.DisabledInstallationDays, Is.EqualTo(32));
        Assert.That(settings.InAppNotificationDays, Is.EqualTo(91));
        Assert.That(settings.BatchSize, Is.EqualTo(250));

        AssertCleanupContract<IPushNotificationMessageRetentionCleanupService>();
        AssertCleanupContract<IDisabledPushInstallationRetentionCleanupService>();
        AssertCleanupContract<IInAppNotificationRetentionCleanupService>();
        AssertJobContract<IPushNotificationMessageRetentionCleanupJob>();
        AssertJobContract<IDisabledPushInstallationRetentionCleanupJob>();
        AssertJobContract<IInAppNotificationRetentionCleanupJob>();
    }

    private static void AssertCleanupContract<TCleanupService>()
    {
        Assert.That(typeof(TCleanupService).GetMethod("CleanupAsync")!.GetParameters()
            .Select(parameter => parameter.ParameterType), Is.EqualTo(new[] { typeof(CancellationToken) }));
    }

    private static void AssertJobContract<TJob>()
    {
        Assert.That(typeof(TJob).GetMethod("ExecuteAsync")!.GetParameters()
            .Select(parameter => parameter.ParameterType), Is.EqualTo(new[] { typeof(CancellationToken) }));
    }

    private static void AssertRetentionCandidateContract<TRepository, TCandidate>(string selectionMethodName)
    {
        var repositoryType = typeof(TRepository);
        var selection = repositoryType.GetMethod(
            selectionMethodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var deletion = repositoryType.GetMethod(
            "RemoveRange",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.That(selection, Is.Not.Null);
        Assert.That(selection!.ReturnType, Is.EqualTo(typeof(Task<IReadOnlyList<TCandidate>>)));
        Assert.That(selection.GetParameters().Select(parameter => (parameter.Name, parameter.ParameterType)), Is.EqualTo(new[] { ("cutoff", typeof(DateTimeOffset)), ("candidateLimit", typeof(int)), ("protectedAccounts", typeof(IReadOnlyList<Id<AccountReference>>)), ("cancellationToken", typeof(CancellationToken)) }));

        Assert.That(deletion, Is.Not.Null);
        Assert.That(deletion!.ReturnType, Is.EqualTo(typeof(void)));
        Assert.That(deletion.GetParameters().Select(parameter => parameter.ParameterType), Is.EqualTo(new[] { typeof(IEnumerable<TCandidate>) }));
    }
}
