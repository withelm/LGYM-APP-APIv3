using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class PaymentsPersistenceLifecycleTests
{
    [Test]
    public void AppDbContext_PaymentsLifecycle_UsesUniqueProviderEventSessionAndClientIdempotencyReferences()
    {
        // Given
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"payments-model-{Id<PaymentCheckout>.New()}")
            .Options;
        using var context = new AppDbContext(options);

        // When
        var checkout = context.Model.FindEntityType("LgymApi.Domain.Entities.PaymentCheckout");
        var receipt = context.Model.FindEntityType("LgymApi.Domain.Entities.PaymentEventReceipt");

        // Then
        checkout.Should().NotBeNull();
        receipt.Should().NotBeNull();
        checkout!.FindProperty("Status").Should().NotBeNull();
        checkout.FindProperty("ClientIdempotencyKey").Should().NotBeNull();
        receipt!.FindProperty("Status").Should().NotBeNull();
        checkout.GetIndexes().Should().ContainSingle(index =>
            index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(new[] { "ProviderSessionReference" }));
        checkout.GetIndexes().Should().ContainSingle(index =>
            index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(new[] { "AccountId", "ClientIdempotencyKey" }));
        receipt.GetIndexes().Should().ContainSingle(index =>
            index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(new[] { "ProviderEventId" }));
    }
}
