namespace LgymApi.Application.Services;

public interface ITokenService
{
    string CreateToken(Guid userId);
}
