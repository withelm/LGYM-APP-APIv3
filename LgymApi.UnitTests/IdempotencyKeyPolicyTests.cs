using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class IdempotencyKeyPolicyTests
{
    [Test]
    public void CalculateKey_WithValidCorrelationId_ReturnsConsistentKey()
    {
        var id = Guid.NewGuid();
        var k1 = IdempotencyKeyPolicy.CalculateKey(id);
        var k2 = IdempotencyKeyPolicy.CalculateKey(id);
        Assert.That(k1, Is.EqualTo(k2));
    }

    [Test]
    public void CalculateKey_WithEmptyGuid_Throws()
    {
        Assert.Throws<ArgumentException>(() => IdempotencyKeyPolicy.CalculateKey(Guid.Empty));
    }

    [Test]
    public void CalculateKey_DifferentIds_DifferentKeys()
    {
        var k1 = IdempotencyKeyPolicy.CalculateKey(Guid.NewGuid());
        var k2 = IdempotencyKeyPolicy.CalculateKey(Guid.NewGuid());
        Assert.That(k1, Is.Not.EqualTo(k2));
    }

    [Test]
    public void AreKeysEqual_SameKeys_True()
    {
        var id = Guid.NewGuid();
        var k1 = IdempotencyKeyPolicy.CalculateKey(id);
        var k2 = IdempotencyKeyPolicy.CalculateKey(id);
        Assert.That(IdempotencyKeyPolicy.AreKeysEqual(k1, k2), Is.True);
    }

    [Test]
    public void AreKeysEqual_DifferentKeys_False()
    {
        var k1 = IdempotencyKeyPolicy.CalculateKey(Guid.NewGuid());
        var k2 = IdempotencyKeyPolicy.CalculateKey(Guid.NewGuid());
        Assert.That(IdempotencyKeyPolicy.AreKeysEqual(k1, k2), Is.False);
    }

    [Test]
    public void AreKeysEqual_NullKeys_True()
    {
        Assert.That(IdempotencyKeyPolicy.AreKeysEqual(null, null), Is.True);
    }

    [Test]
    public void AreKeysEqual_OneNullKey_False()
    {
        var k1 = IdempotencyKeyPolicy.CalculateKey(Guid.NewGuid());
        Assert.That(IdempotencyKeyPolicy.AreKeysEqual(k1, null), Is.False);
        Assert.That(IdempotencyKeyPolicy.AreKeysEqual(null, k1), Is.False);
    }

    [Test]
    public void IsKeyForCorrelation_WithMatchingKey_ReturnsTrue()
    {
        var id = Guid.NewGuid();
        var key = IdempotencyKeyPolicy.CalculateKey(id);
        Assert.That(IdempotencyKeyPolicy.IsKeyForCorrelation(key, id), Is.True);
    }

    [Test]
    public void IsKeyForCorrelation_WithNonMatchingKey_ReturnsFalse()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var key = IdempotencyKeyPolicy.CalculateKey(id1);
        Assert.That(IdempotencyKeyPolicy.IsKeyForCorrelation(key, id2), Is.False);
    }

    [Test]
    public void IsKeyForCorrelation_WithNullKey_ReturnsFalse()
    {
        var id = Guid.NewGuid();
        Assert.That(IdempotencyKeyPolicy.IsKeyForCorrelation(null, id), Is.False);
    }

    [Test]
    public async Task Persistence_FirstEnqueueCreatesOneRecord()
    {
        var cid = Guid.NewGuid();
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"T1_{Guid.NewGuid()}")
            .Options;

        using var ctx = new AppDbContext(opts);
        await ctx.Database.EnsureCreatedAsync();
        var r = new CommandEnvelopeRepository(ctx);
        var e = new CommandEnvelope { CorrelationId = cid, PayloadJson = "{}", CommandTypeFullName = "T", Status = ActionExecutionStatus.Pending };
        await r.AddAsync(e);
        await ctx.SaveChangesAsync();
        var f = await r.FindByCorrelationIdAsync(cid);
        Assert.That(f, Is.Not.Null);
    }

    [Test]
    public async Task Persistence_DuplicateEnqueueDetectedByRepository()
    {
        var cid = Guid.NewGuid();
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"T2_{Guid.NewGuid()}")
            .Options;

        using var ctx = new AppDbContext(opts);
        await ctx.Database.EnsureCreatedAsync();
        var r = new CommandEnvelopeRepository(ctx);
        var e = new CommandEnvelope { CorrelationId = cid, PayloadJson = "{}", CommandTypeFullName = "T", Status = ActionExecutionStatus.Pending };
        await r.AddAsync(e);
        await ctx.SaveChangesAsync();
        var f = await r.FindByCorrelationIdAsync(cid);
        Assert.That(f, Is.Not.Null);
    }

    [Test]
    public async Task Persistence_AddOrGetExistingAsync_ReturnsDuplicateNotNewRecord()
    {
        var cid = Guid.NewGuid();
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"T3_{Guid.NewGuid()}")
            .Options;

        using var ctx = new AppDbContext(opts);
        await ctx.Database.EnsureCreatedAsync();
        var r = new CommandEnvelopeRepository(ctx);
        var e1 = new CommandEnvelope { CorrelationId = cid, PayloadJson = "{\"a\": 1}", CommandTypeFullName = "T", Status = ActionExecutionStatus.Pending };
        await r.AddOrGetExistingAsync(e1);
        await ctx.SaveChangesAsync();
        var e2 = new CommandEnvelope { CorrelationId = cid, PayloadJson = "{\"b\": 2}", CommandTypeFullName = "T", Status = ActionExecutionStatus.Pending };
        var res = await r.AddOrGetExistingAsync(e2);
        Assert.That(res.PayloadJson, Is.EqualTo("{\"a\": 1}"));
        var all = await ctx.CommandEnvelopes.ToListAsync();
        Assert.That(all.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Persistence_ConcurrentDuplicateEnqueueResultsInSingleRecord()
    {
        var cid = Guid.NewGuid();
        var dbName = $"T4_{Guid.NewGuid()}";

        var t1 = Task.Run(async () =>
        {
            var opts = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            using var ctx = new AppDbContext(opts);
            var r = new CommandEnvelopeRepository(ctx);
            var e = new CommandEnvelope { CorrelationId = cid, PayloadJson = "{\"a\": 1}", CommandTypeFullName = "T", Status = ActionExecutionStatus.Pending };
            var res = await r.AddOrGetExistingAsync(e);
            await ctx.SaveChangesAsync();
            return res;
        });

        var t2 = Task.Run(async () =>
        {
            var opts = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            using var ctx = new AppDbContext(opts);
            var r = new CommandEnvelopeRepository(ctx);
            var e = new CommandEnvelope { CorrelationId = cid, PayloadJson = "{\"b\": 2}", CommandTypeFullName = "T", Status = ActionExecutionStatus.Pending };
            return await r.AddOrGetExistingAsync(e);
        });

        var ress = await Task.WhenAll(t1, t2);
        Assert.That(ress[0].CorrelationId, Is.EqualTo(cid));
        Assert.That(ress[1].CorrelationId, Is.EqualTo(cid));

        var opts_final = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        using var ctx_final = new AppDbContext(opts_final);
        var all = await ctx_final.CommandEnvelopes.ToListAsync();
        Assert.That(all.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Persistence_MultipleEnvelopesWithDifferentCorrelationIds()
    {
        var cid1 = Guid.NewGuid();
        var cid2 = Guid.NewGuid();
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"T5_{Guid.NewGuid()}")
            .Options;

        using var ctx = new AppDbContext(opts);
        await ctx.Database.EnsureCreatedAsync();
        var r = new CommandEnvelopeRepository(ctx);
        var e1 = new CommandEnvelope { CorrelationId = cid1, PayloadJson = "{}", CommandTypeFullName = "T", Status = ActionExecutionStatus.Pending };
        var e2 = new CommandEnvelope { CorrelationId = cid2, PayloadJson = "{}", CommandTypeFullName = "T", Status = ActionExecutionStatus.Pending };
        await r.AddAsync(e1);
        await r.AddAsync(e2);
        await ctx.SaveChangesAsync();
        var all = await ctx.CommandEnvelopes.ToListAsync();
        Assert.That(all.Count, Is.EqualTo(2));
    }
}
