using System.Collections.Concurrent;
using LgymApi.Application.Services;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;

namespace LgymApi.Infrastructure.Services;

public sealed class UserSessionCache : IUserSessionCache
{
    private const int DefaultCapacity = 1000;
    private readonly int _capacity;

    private readonly ConcurrentDictionary<Id<User>, LinkedListNode<Id<User>>> _nodes = new();
    private readonly LinkedList<Id<User>> _lru = new();
    private readonly object _sync = new();

    public UserSessionCache(IConfiguration configuration)
    {
        var configuredValue = configuration["UserSessionCache:Capacity"];
        _capacity = int.TryParse(configuredValue, out var parsedCapacity) ? parsedCapacity : DefaultCapacity;
        if (_capacity <= 0)
        {
            _capacity = DefaultCapacity;
        }
    }

    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _lru.Count;
            }
        }
    }

    public void AddOrRefresh(Id<User> userId)
    {
        if (userId.IsEmpty)
        {
            return;
        }

        lock (_sync)
        {
            if (_nodes.TryGetValue(userId, out var existingNode))
            {
                _lru.Remove(existingNode);
                _lru.AddFirst(existingNode);
                return;
            }

            var node = new LinkedListNode<Id<User>>(userId);
            _lru.AddFirst(node);
            _nodes[userId] = node;

            if (_lru.Count <= _capacity)
            {
                return;
            }

            var leastRecent = _lru.Last;
            if (leastRecent == null)
            {
                return;
            }

            _lru.RemoveLast();
            _nodes.TryRemove(leastRecent.Value, out _);
        }
    }

    public bool Remove(Id<User> userId)
    {
        if (userId.IsEmpty)
        {
            return false;
        }

        lock (_sync)
        {
            if (!_nodes.TryRemove(userId, out var node))
            {
                return false;
            }

            _lru.Remove(node);
            return true;
        }
    }

    public bool Contains(Id<User> userId)
    {
        if (userId.IsEmpty)
        {
            return false;
        }

        return _nodes.ContainsKey(userId);
    }
}
