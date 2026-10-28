using FluentAssertions;
using LgymApi.Infrastructure.Data;
using LgymApi.Payments.Persistence;
using LgymApi.Payments.Providers.Stripe;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class PaymentsCompositionTests
{
    [Test]
    public void OrderedHostComposition_RegistersPaymentsFacadeAndSharedPersistenceBridgeExactlyOnce()
    {
        var services = CompositionRootTestHost.Create();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        services.Should().ContainSingle(descriptor => descriptor.ServiceType == typeof(IPaymentCheckoutRepository));
        services.Should().ContainSingle(descriptor => descriptor.ServiceType == typeof(IPaymentEventReceiptRepository));
        services.Should().ContainSingle(descriptor => descriptor.ServiceType == typeof(StripePaymentsService));

        var appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var paymentsContext = scope.ServiceProvider.GetRequiredService<IPaymentsPersistenceContext>();

        paymentsContext.Should().BeSameAs(appDbContext);
        scope.ServiceProvider.GetServices<IPaymentCheckoutRepository>().Should().ContainSingle();
        scope.ServiceProvider.GetServices<IPaymentEventReceiptRepository>().Should().ContainSingle();
    }
}
