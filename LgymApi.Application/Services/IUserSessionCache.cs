using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Services;

public interface IUserSessionCache
{
    void AddOrRefresh(Id<User> userId);
    bool Remove(Id<User> userId);
    bool Contains(Id<User> userId);
    int Count { get; }
}
