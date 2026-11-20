using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.BackgroundWorker.Actions.Contracts;
using LgymApi.BackgroundWorker.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class BackgroundActionStartupValidationTests
{
    [Test]
    public void Validate_AcceptsExactlyNineteenRowsAndTwentyHandlers()
    {
        var registry = CommandContractRegistry.CreateDefault();
        var services = CreateValidServices(registry);

        var action = () => BackgroundActionRegistrationValidator.Validate(services, registry);

        Assert.That(() => { action(); }, Throws.Nothing);
        Assert.That(registry.Contracts, Has.Exactly(19).Items);
        Assert.That(registry.Contracts.Sum(contract => contract.ExpectedHandlerTypes.Count), Is.EqualTo(20));
    }

    [Test]
    public void Validate_RejectsMissingHandlerDescriptor()
    {
        var registry = CommandContractRegistry.CreateDefault();
        var services = CreateValidServices(registry);
        var contract = registry.Contracts.First(candidate => candidate.ExpectedHandlerTypes.Count == 1);
        var handlerType = contract.ExpectedHandlerTypes.Single();
        services.Remove(services.Single(descriptor =>
            descriptor.ServiceType == typeof(IBackgroundAction<>).MakeGenericType(contract.RuntimeType)
            && descriptor.ImplementationType == handlerType));

        var action = () => BackgroundActionRegistrationValidator.Validate(services, registry);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape($"*{contract.CanonicalId}*{handlerType.FullName}*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void Validate_RejectsDuplicateHandlerDescriptor()
    {
        var registry = CommandContractRegistry.CreateDefault();
        var services = CreateValidServices(registry);
        var contract = registry.Contracts.First(candidate => candidate.ExpectedHandlerTypes.Count == 1);
        var handlerType = contract.ExpectedHandlerTypes.Single();
        services.AddScoped(typeof(IBackgroundAction<>).MakeGenericType(contract.RuntimeType), handlerType);

        var action = () => BackgroundActionRegistrationValidator.Validate(services, registry);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape($"*{contract.CanonicalId}*{handlerType.FullName}*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void Validate_RejectsExtraHandlerDescriptor()
    {
        var registry = CommandContractRegistry.CreateDefault();
        var services = CreateValidServices(registry);
        services.AddScoped<IBackgroundAction<ExtraCommand>, ExtraHandler>();

        var action = () => BackgroundActionRegistrationValidator.Validate(services, registry);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape($"*{typeof(ExtraCommand).FullName}*{typeof(ExtraHandler).FullName}*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void Validate_RejectsSwappedHandlerDescriptors()
    {
        var registry = CommandContractRegistry.CreateDefault();
        var services = CreateValidServices(registry);
        var first = registry.Contracts.First(candidate => candidate.ExpectedHandlerTypes.Count == 1);
        var second = registry.Contracts.Skip(1).First(candidate => candidate.ExpectedHandlerTypes.Count == 1);
        var firstHandler = first.ExpectedHandlerTypes.Single();
        var secondHandler = second.ExpectedHandlerTypes.Single();
        services.Remove(services.Single(descriptor => descriptor.ServiceType ==
            typeof(IBackgroundAction<>).MakeGenericType(first.RuntimeType)));
        services.Remove(services.Single(descriptor => descriptor.ServiceType ==
            typeof(IBackgroundAction<>).MakeGenericType(second.RuntimeType)));
        services.AddScoped(typeof(IBackgroundAction<>).MakeGenericType(first.RuntimeType), secondHandler);
        services.AddScoped(typeof(IBackgroundAction<>).MakeGenericType(second.RuntimeType), firstHandler);

        var action = () => BackgroundActionRegistrationValidator.Validate(services, registry);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape($"*{first.CanonicalId}*{firstHandler.FullName}*{secondHandler.FullName}*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    private static ServiceCollection CreateValidServices(CommandContractRegistry registry)
    {
        var services = new ServiceCollection();
        foreach (var contract in registry.Contracts)
        {
            foreach (var handlerType in contract.ExpectedHandlerTypes)
            {
                services.AddScoped(typeof(IBackgroundAction<>).MakeGenericType(contract.RuntimeType), handlerType);
            }
        }

        return services;
    }

    private sealed class ExtraCommand : IActionCommand;

    private sealed class ExtraHandler : IBackgroundAction<ExtraCommand>
    {
        public Task ExecuteAsync(ExtraCommand command, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
