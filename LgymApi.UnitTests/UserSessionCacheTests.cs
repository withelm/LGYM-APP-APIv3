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
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();

        cache.AddOrRefresh((Id<User>)first);
        cache.AddOrRefresh((Id<User>)second);
        cache.AddOrRefresh((Id<User>)third);

        Assert.Multiple(() =>
        {
            Assert.That(cache.Count, Is.EqualTo(2));
            Assert.That(cache.Contains((Id<User>)first), Is.False);
            Assert.That(cache.Contains((Id<User>)second), Is.True);
            Assert.That(cache.Contains((Id<User>)third), Is.True);
        });
    }

    [Test]
    public void AddOrRefresh_RefreshesExistingItem()
    {
        var cache = CreateCache(capacity: 2);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();

        cache.AddOrRefresh((Id<User>)first);
        cache.AddOrRefresh((Id<User>)second);
        cache.AddOrRefresh((Id<User>)first);
        cache.AddOrRefresh((Id<User>)third);

        Assert.Multiple(() =>
        {
            Assert.That(cache.Contains((Id<User>)first), Is.True);
            Assert.That(cache.Contains((Id<User>)second), Is.False);
            Assert.That(cache.Contains((Id<User>)third), Is.True);
        });
    }

    [Test]
    public void Remove_ReturnsExpectedValues()
    {
        var cache = CreateCache(capacity: 2);
        var userId = Guid.NewGuid();
        cache.AddOrRefresh((Id<User>)userId);

        Assert.Multiple(() =>
        {
            Assert.That(cache.Remove(Id<User>.Empty), Is.False);
            Assert.That(cache.Remove((Id<User>)Guid.NewGuid()), Is.False);
            Assert.That(cache.Remove((Id<User>)userId), Is.True);
            Assert.That(cache.Contains((Id<User>)userId), Is.False);
        });
    }

    [Test]
    public void Constructor_UsesDefaultCapacity_ForInvalidConfiguredValue()
    {
        var cache = CreateCache(capacityRaw: "0");

        for (var i = 0; i < 1001; i++)
        {
            cache.AddOrRefresh((Id<User>)Guid.NewGuid());
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
