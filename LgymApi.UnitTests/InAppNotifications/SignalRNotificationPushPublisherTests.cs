using FluentAssertions;
using LgymApi.Api.Features.InAppNotification;
using LgymApi.Api.Hubs;
using LgymApi.Application.Notifications.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Reflection;
using NUnit.Framework;

namespace LgymApi.UnitTests.InAppNotifications;

[TestFixture]
public sealed class SignalRNotificationPushPublisherTests
{
    [Test]
    public async Task PushAsync_Success_SendsNotificationToGroup()
    {
        var (publisher, clientProxy, logger) = CreatePublisher(throwOnSend: false);
        var notification = CreateNotification();

         await InvokePushAsync(publisher, notification);

         clientProxy.SendCalls.Should().Be(1);
         clientProxy.LastMethod.Should().Be("ReceiveNotification");
         ReferenceEquals(clientProxy.LastArgs![0], notification).Should().BeTrue();
         logger.WarningCalls.Should().Be(0);
    }

    [Test]
    public async Task PushAsync_Failure_LogsWarning()
    {
        var (publisher, _, logger) = CreatePublisher(throwOnSend: true);
        var notification = CreateNotification();

         await InvokePushAsync(publisher, notification);

         logger.WarningCalls.Should().Be(1);
    }

    private static (object Publisher, FakeClientProxy ClientProxy, FakeLogger Logger) CreatePublisher(bool throwOnSend)
    {
        var clientProxy = new FakeClientProxy(throwOnSend);
        var hubClients = new FakeHubClients(clientProxy);
        var hubContext = new FakeHubContext(hubClients);
        var logger = new FakeLogger();
        var type = typeof(NotificationHub).Assembly.GetType("LgymApi.Api.Features.InAppNotification.SignalRNotificationPushPublisher", throwOnError: true)!;
        var publisher = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new object[] { hubContext, logger }, null)!;
        return (publisher, clientProxy, logger);
    }

    private static InAppNotificationResult CreateNotification()
        => new(Id<InAppNotification>.New(), Id<User>.New(), "Hello", "/trainers/dashboard", false, InAppNotificationTypes.InvitationSent, false, null, DateTimeOffset.UtcNow);

    private static async Task InvokePushAsync(object publisher, InAppNotificationResult notification)
    {
        var method = publisher.GetType().GetMethod("PushAsync", BindingFlags.Instance | BindingFlags.Public)!;
        await (Task)method.Invoke(publisher, new object[] { notification, CancellationToken.None })!;
    }

    private sealed class FakeClientProxy : IClientProxy
    {
        private readonly bool _throwOnSend;

        public FakeClientProxy(bool throwOnSend) => _throwOnSend = throwOnSend;

        public int SendCalls { get; private set; }
        public string? LastMethod { get; private set; }
        public object?[]? LastArgs { get; private set; }

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            SendCalls++;
            LastMethod = method;
            LastArgs = args;
            if (_throwOnSend)
            {
                throw new InvalidOperationException("send failed");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeHubClients : IHubClients
    {
        private readonly IClientProxy _proxy;

        public FakeHubClients(IClientProxy proxy) => _proxy = proxy;
        public IClientProxy All => _proxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => _proxy;
        public IClientProxy Client(string connectionId) => _proxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => _proxy;
        public IClientProxy Group(string groupName) => _proxy;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => _proxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => _proxy;
        public IClientProxy User(string userId) => _proxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => _proxy;
    }

    private sealed class FakeHubContext : IHubContext<NotificationHub>
    {
        public FakeHubContext(IHubClients clients) => Clients = clients;
        public IHubClients Clients { get; }
        public IGroupManager Groups { get; } = new FakeGroupManager();
    }

    private sealed class FakeGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeLogger : ILogger<SignalRNotificationPushPublisher>
    {
        public int WarningCalls { get; private set; }
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                WarningCalls++;
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
