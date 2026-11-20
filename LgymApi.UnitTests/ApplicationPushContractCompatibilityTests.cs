using System.Reflection;
using System.Text.Json;
using LgymApi.Application.Platform.Contracts.Serialization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using ApplicationPushBackgroundScheduler = LgymApi.Application.Notifications.Contracts.Push.IPushBackgroundScheduler;
using ApplicationPushDeliveryRetrySettings = LgymApi.Application.Notifications.Contracts.Push.IPushNotificationDeliveryRetrySettings;
using ApplicationPushEventPayload = LgymApi.Application.Notifications.Contracts.Push.PushEventPayload;
using ApplicationPushProviderSender = LgymApi.Application.Notifications.Contracts.Push.IPushProviderSender;
using ApplicationPushInstallationDeliveryTarget = LgymApi.Application.Notifications.Contracts.Push.PushInstallationDeliveryTarget;
using ApplicationPushSendAttemptResult = LgymApi.Application.Notifications.Contracts.Push.PushSendAttemptResult;
using ApplicationPushSendOutcome = LgymApi.Application.Notifications.Contracts.Push.PushSendOutcome;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ApplicationPushContractCompatibilityTests
{
    private const string PushContractsNamespace = "LgymApi.Application.Notifications.Contracts.Push";
    private const string NotificationIdText = "01234567-89ab-cdef-0123-456789abcdef";
    private const string ExpectedPayloadJson = "{\"schemaVersion\":1,\"type\":\"trainer.note.updated\",\"eventId\":\"event-contract-1\",\"entityId\":\"entity-contract-1\",\"inAppNotificationId\":\"01234567-89ab-cdef-0123-456789abcdef\",\"deeplink\":\"/notifications/event-contract-1\"}";

    private static readonly string[] PayloadMemberNames =
    [
        nameof(ApplicationPushEventPayload.SchemaVersion),
        nameof(ApplicationPushEventPayload.Type),
        nameof(ApplicationPushEventPayload.EventId),
        nameof(ApplicationPushEventPayload.EntityId),
        nameof(ApplicationPushEventPayload.InAppNotificationId),
        nameof(ApplicationPushEventPayload.Deeplink)
    ];

    private static readonly string[] ResultMemberNames =
    [
        nameof(ApplicationPushSendAttemptResult.Outcome),
        nameof(ApplicationPushSendAttemptResult.ProviderStatus),
        nameof(ApplicationPushSendAttemptResult.ProviderMessageId),
        nameof(ApplicationPushSendAttemptResult.ProviderErrorCode),
        nameof(ApplicationPushSendAttemptResult.ProviderResponseSummary)
    ];

    [Test]
    public void ApplicationPushContracts_ExposeOnlyTheExactNotificationsOwnedTypes()
    {
        var notificationsAssembly = typeof(ApplicationPushEventPayload).Assembly;
        var contractTypes = notificationsAssembly
            .GetExportedTypes()
            .Where(type => type.Namespace == PushContractsNamespace)
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.That(contractTypes.Select(type => type.FullName), Is.EqualTo(new[] { $"{PushContractsNamespace}.IPushBackgroundScheduler", $"{PushContractsNamespace}.IPushNotificationDeliveryRetrySettings", $"{PushContractsNamespace}.IPushProviderSender", $"{PushContractsNamespace}.PushEventPayload", $"{PushContractsNamespace}.PushInstallationDeliveryTarget", $"{PushContractsNamespace}.PushSendAttemptResult", $"{PushContractsNamespace}.PushSendOutcome" }));
        Assert.That((contractTypes).All(type => type.Assembly == notificationsAssembly && type.IsPublic), Is.True);
    }

    [Test]
    public void ApplicationPushScheduler_KeepsExactPublicMethodSignatures()
    {
        AssertInterfaceShape(typeof(ApplicationPushBackgroundScheduler), expectedMethodCount: 2);
        var schedulerMethods = typeof(ApplicationPushBackgroundScheduler)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OrderBy(method => method.MetadataToken)
            .ToArray();

        AssertMethod(
            schedulerMethods[0],
            "Enqueue",
            typeof(string),
            ("notificationId", typeof(string), false));
        AssertMethod(
            schedulerMethods[1],
            "ScheduleRetry",
            typeof(string),
            ("notificationId", typeof(string), false),
            ("delay", typeof(TimeSpan), false));

        var nullability = new NullabilityInfoContext();
        Assert.That(nullability.Create(schedulerMethods[0].ReturnParameter).ReadState, Is.EqualTo(NullabilityState.Nullable));
        Assert.That(nullability.Create(schedulerMethods[1].ReturnParameter).ReadState, Is.EqualTo(NullabilityState.Nullable));
    }

    [Test]
    public void ApplicationPushProviderPort_AndRetrySettings_RemainProviderNeutral()
    {
        AssertInterfaceShape(typeof(ApplicationPushProviderSender), expectedMethodCount: 1);
        var sendMethod = typeof(ApplicationPushProviderSender).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Single();
        AssertMethod(
            sendMethod,
            "SendAsync",
            typeof(Task<ApplicationPushSendAttemptResult>),
            ("target", typeof(ApplicationPushInstallationDeliveryTarget), false),
            ("payload", typeof(ApplicationPushEventPayload), false),
            ("cancellationToken", typeof(CancellationToken), true));

        var retrySettingsType = typeof(ApplicationPushDeliveryRetrySettings);
        Assert.That(retrySettingsType.IsPublic, Is.True);
        Assert.That(retrySettingsType.IsInterface, Is.True);
        Assert.That(retrySettingsType.IsGenericType, Is.False);
        Assert.That(retrySettingsType.GetInterfaces(), Is.Empty);
        Assert.That(retrySettingsType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.Name), Is.EqualTo(new[] { "get_RetryDelaysSeconds" }));
        Assert.That(retrySettingsType.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), Is.Empty);
        var retryDelays = retrySettingsType
            .GetProperty(nameof(ApplicationPushDeliveryRetrySettings.RetryDelaysSeconds));
        Assert.That(retryDelays, Is.Not.Null);
        Assert.That(retryDelays!.PropertyType, Is.EqualTo(typeof(IReadOnlyList<int>)));
        Assert.That(retrySettingsType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(property => property.Name), Is.EqualTo(new[] { nameof(ApplicationPushDeliveryRetrySettings.RetryDelaysSeconds) }));
    }

    [Test]
    public void ApplicationPushEventPayload_PreservesMemberOrderTypedIdBoundaryAndGoldenJson()
    {
        Assert.That(Id<InAppNotification>.TryParse(NotificationIdText, out var notificationId), Is.True);
        var applicationPayload = new ApplicationPushEventPayload(
            1,
            "trainer.note.updated",
            "event-contract-1",
            "entity-contract-1",
            notificationId,
            "/notifications/event-contract-1");

        AssertRecordShape<ApplicationPushEventPayload>(
            PayloadMemberNames,
            typeof(int),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(Id<InAppNotification>?),
            typeof(string));
        AssertPropertyNullability<ApplicationPushEventPayload>(
            (nameof(ApplicationPushEventPayload.Type), NullabilityState.NotNull),
            (nameof(ApplicationPushEventPayload.EventId), NullabilityState.NotNull),
            (nameof(ApplicationPushEventPayload.EntityId), NullabilityState.Nullable),
            (nameof(ApplicationPushEventPayload.InAppNotificationId), NullabilityState.Nullable),
            (nameof(ApplicationPushEventPayload.Deeplink), NullabilityState.Nullable));

        var applicationJson = JsonSerializer.Serialize(applicationPayload, SharedSerializationOptions.Current);

        Assert.That(applicationJson, Is.EqualTo(ExpectedPayloadJson));
        Assert.That(JsonSerializer.Deserialize<ApplicationPushEventPayload>(ExpectedPayloadJson, SharedSerializationOptions.Current), Is.EqualTo(applicationPayload));
    }

    [Test]
    public void ApplicationPushSendAttemptResult_PreservesMemberOrderAndOutcomeValues()
    {
        AssertRecordShape<ApplicationPushSendAttemptResult>(
            ResultMemberNames,
            typeof(ApplicationPushSendOutcome),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string));
        AssertPropertyNullability<ApplicationPushSendAttemptResult>(
            (nameof(ApplicationPushSendAttemptResult.ProviderStatus), NullabilityState.NotNull),
            (nameof(ApplicationPushSendAttemptResult.ProviderMessageId), NullabilityState.Nullable),
            (nameof(ApplicationPushSendAttemptResult.ProviderErrorCode), NullabilityState.Nullable),
            (nameof(ApplicationPushSendAttemptResult.ProviderResponseSummary), NullabilityState.Nullable));

        Assert.That(Enum.GetNames<ApplicationPushSendOutcome>(), Is.EqualTo(new[] { nameof(ApplicationPushSendOutcome.Sent), nameof(ApplicationPushSendOutcome.TransientFailure), nameof(ApplicationPushSendOutcome.InvalidToken), nameof(ApplicationPushSendOutcome.PermanentFailure), nameof(ApplicationPushSendOutcome.Skipped) }));
        Assert.That(Enum.GetValues<ApplicationPushSendOutcome>().Select(outcome => (int)outcome), Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
    }

    private static void AssertInterfaceShape(Type type, int expectedMethodCount)
    {
        Assert.That(type.IsPublic, Is.True);
        Assert.That(type.IsInterface, Is.True);
        Assert.That(type.IsGenericType, Is.False);
        Assert.That(type.GetInterfaces(), Is.Empty);
        Assert.That(type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), Is.Empty);
        Assert.That(type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), Is.Empty);
        Assert.That(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), Has.Exactly(expectedMethodCount).Items);
    }

    private static void AssertMethod(
        MethodInfo method,
        string expectedName,
        Type expectedReturnType,
        params (string Name, Type Type, bool IsOptional)[] expectedParameters)
    {
        Assert.That(method.Name, Is.EqualTo(expectedName));
        Assert.That(method.ReturnType, Is.EqualTo(expectedReturnType));
        Assert.That(method.IsGenericMethod, Is.False);

        var parameters = method.GetParameters();
        Assert.That(parameters.Select(parameter => parameter.Name), Is.EqualTo(expectedParameters.Select(parameter => parameter.Name)));
        Assert.That(parameters.Select(parameter => parameter.ParameterType), Is.EqualTo(expectedParameters.Select(parameter => parameter.Type)));
        Assert.That(parameters.Select(parameter => parameter.IsOptional), Is.EqualTo(expectedParameters.Select(parameter => parameter.IsOptional)));
    }

    private static void AssertRecordShape<T>(string[] expectedMemberNames, params Type[] expectedMemberTypes)
    {
        var type = typeof(T);
        var properties = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OrderBy(property => property.MetadataToken)
            .ToArray();
        var assertedValue84 = (type.GetConstructors()).ToArray();
        Assert.That(assertedValue84, Has.Exactly(1).Items);
        var constructor = assertedValue84.Single();

        Assert.That(type.IsPublic, Is.True);
        Assert.That(type.IsSealed, Is.True);
        Assert.That(properties.Select(property => property.Name), Is.EqualTo(expectedMemberNames));
        Assert.That(properties.Select(property => property.PropertyType), Is.EqualTo(expectedMemberTypes));
        Assert.That(constructor.GetParameters().Select(parameter => parameter.Name), Is.EqualTo(expectedMemberNames));
        Assert.That(constructor.GetParameters().Select(parameter => parameter.ParameterType), Is.EqualTo(expectedMemberTypes));
    }

    private static void AssertPropertyNullability<T>(params (string Name, NullabilityState State)[] expectedProperties)
    {
        var nullability = new NullabilityInfoContext();
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (var expectedProperty in expectedProperties)
        {
            var property = properties.Single(candidate => candidate.Name == expectedProperty.Name);
            Assert.That(nullability.Create(property).ReadState, Is.EqualTo(expectedProperty.State));
        }
    }
}
