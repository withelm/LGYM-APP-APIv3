using LgymApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Payments.Persistence;

internal interface IPaymentsPersistenceContext
{
    DbSet<PaymentCheckout> PaymentCheckouts { get; }
    DbSet<PaymentEventReceipt> PaymentEventReceipts { get; }
}
