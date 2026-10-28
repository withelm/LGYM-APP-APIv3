using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Payments.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LgymApi.IntegrationTests;

[TestFixture]
[NonParallelizable]
[Category("PostgreSql")]
public sealed class PostgreSqlPaymentsPersistenceTests : PostgreSqlIntegrationTestBase
{
    [Test]
    public async Task PaymentReferences_RejectDuplicateProviderAndClientIdempotencyKeys_AndRollbackLeavesNoPartialRecord()
    {
        // Given
        var account = await SeedUserAsync($"payments-{Id<User>.New()}", $"payments-{Id<User>.New()}@test.local");
        var checkout = new PaymentCheckout
        {
            Id = Id<PaymentCheckout>.New(),
            AccountId = account.Id,
            ClientIdempotencyKey = "client-proof",
            ProviderSessionReference = "session-proof",
            Status = PaymentCheckoutStatus.Paid
        };

        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            IPaymentsPersistenceContext payments = context;
            payments.PaymentCheckouts.Add(checkout);
            payments.PaymentEventReceipts.Add(new PaymentEventReceipt
            {
                Id = Id<PaymentEventReceipt>.New(),
                PaymentCheckoutId = checkout.Id,
                ProviderEventId = "event-proof",
                Status = PaymentEventReceiptStatus.Processed
            });
            await context.SaveChangesAsync();
        }

        // When
        using (var duplicateScope = Factory.Services.CreateScope())
        {
            var context = duplicateScope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.PaymentCheckouts.Add(new PaymentCheckout
            {
                Id = Id<PaymentCheckout>.New(),
                AccountId = account.Id,
                ClientIdempotencyKey = "client-other",
                ProviderSessionReference = "session-proof",
                Status = PaymentCheckoutStatus.Created
            });
            await AssertUniqueViolationAsync(context, "IX_PaymentCheckouts_ProviderSessionReference");

            context.PaymentCheckouts.Add(new PaymentCheckout
            {
                Id = Id<PaymentCheckout>.New(),
                AccountId = account.Id,
                ClientIdempotencyKey = "client-proof",
                ProviderSessionReference = "session-other",
                Status = PaymentCheckoutStatus.Created
            });
            await AssertUniqueViolationAsync(context, "IX_PaymentCheckouts_AccountId_ClientIdempotencyKey");

            context.PaymentEventReceipts.Add(new PaymentEventReceipt
            {
                Id = Id<PaymentEventReceipt>.New(),
                PaymentCheckoutId = checkout.Id,
                ProviderEventId = "event-proof",
                Status = PaymentEventReceiptStatus.Received
            });
            await AssertUniqueViolationAsync(context, "IX_PaymentEventReceipts_ProviderEventId");
        }

        var rollbackSessionReference = "session-rollback";
        using (var rollbackScope = Factory.Services.CreateScope())
        {
            var context = rollbackScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var transaction = await context.Database.BeginTransactionAsync();
            context.PaymentCheckouts.Add(new PaymentCheckout
            {
                Id = Id<PaymentCheckout>.New(),
                AccountId = account.Id,
                ClientIdempotencyKey = "client-rollback",
                ProviderSessionReference = rollbackSessionReference,
                Status = PaymentCheckoutStatus.Created
            });
            await context.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        // Then
        using var readScope = Factory.Services.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await readContext.PaymentCheckouts.CountAsync(item => item.ProviderSessionReference == "session-proof")).Should().Be(1);
        (await readContext.PaymentEventReceipts.CountAsync(item => item.ProviderEventId == "event-proof")).Should().Be(1);
        (await readContext.PaymentCheckouts.CountAsync(item => item.ProviderSessionReference == rollbackSessionReference)).Should().Be(0);
    }

    private static async Task AssertUniqueViolationAsync(AppDbContext context, string constraintName)
    {
        Func<Task> save = async () => await context.SaveChangesAsync();
        var exception = await save.Should().ThrowAsync<DbUpdateException>();
        var postgres = exception.Which.InnerException.Should().BeOfType<PostgresException>().Which;
        postgres.SqlState.Should().Be("23505");
        postgres.ConstraintName.Should().Be(constraintName);
        context.ChangeTracker.Clear();
    }
}
