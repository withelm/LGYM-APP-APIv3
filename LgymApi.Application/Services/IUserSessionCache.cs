namespace LgymApi.Application.Services;

public interface IUserSessionCache
{
    void AddOrRefresh(Guid userId);
    bool Remove(Guid userId);
    bool Contains(Guid userId);
    int Count { get; }
}
