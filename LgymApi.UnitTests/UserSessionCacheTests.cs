using LgymApi.Infrastructure.Services;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class UserSessionCacheTests
{
    [Test]
    public void AddOrRefresh_IgnoresEmptyGuid()
    {
        var cache = CreateCache(capacity: 2);

        cache.AddOrRefresh(Id<User>.Empty);

        Assert.That(cache.Count, Is.EqualTo(0));
    }

    [Test]
    public void AddOrRefresh_EvictsLeastRecentlyUsed_WhenCapacityExceeded()
    {
        var cache = CreateCache(capacity: 2);
        var first = Id<User>.New();
        var second = Id<User>.New();
        var third = Id<User>.New();

        cache.AddOrRefresh(first);
        cache.AddOrRefresh(second);
        cache.AddOrRefresh(third);

        Assert.Multiple(() =>
        {
            Assert.That(cache.Count, Is.EqualTo(2));
            Assert.That(cache.Contains(first), Is.False);
            Assert.That(cache.Contains(second), Is.True);
            Assert.That(cache.Contains(third), Is.True);
        });
    }

    [Test]
    public void AddOrRefresh_RefreshesExistingItem()
    {
        var cache = CreateCache(capacity: 2);
        var first = Id<User>.New();
        var second = Id<User>.New();
        var third = Id<User>.New();

        cache.AddOrRefresh(first);
        cache.AddOrRefresh(second);
        cache.AddOrRefresh(first);
        cache.AddOrRefresh(third);

        Assert.Multiple(() =>
        {
            Assert.That(cache.Contains(first), Is.True);
            Assert.That(cache.Contains(second), Is.False);
            Assert.That(cache.Contains(third), Is.True);
        });
    }

    [Test]
    public void Remove_ReturnsExpectedValues()
    {
        var cache = CreateCache(capacity: 2);
        var userId = Id<User>.New();
        cache.AddOrRefresh(userId);

        Assert.Multiple(() =>
        {
            Assert.That(cache.Remove(Id<User>.Empty), Is.False);
            Assert.That(cache.Remove(Id<User>.New()), Is.False);
            Assert.That(cache.Remove(userId), Is.True);
            Assert.That(cache.Contains(userId), Is.False);
        });
    }

    [Test]
    public void Constructor_UsesDefaultCapacity_ForInvalidConfiguredValue()
    {
        var cache = CreateCache(capacityRaw: "0");

        for (var i = 0; i < 1001; i++)
        {
            cache.AddOrRefresh(Id<User>.New());
        }

        Assert.That(cache.Count, Is.EqualTo(1000));
    }

    private static UserSessionCache CreateCache(int? capacity = null, string? capacityRaw = null)
    {
        var value = capacityRaw ?? capacity?.ToString();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UserSessionCache:Capacity"] = value
            })
            .Build();

        return new UserSessionCache(configuration);
    }
}
